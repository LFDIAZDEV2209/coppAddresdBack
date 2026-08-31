using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Calculador del Índice de Transformación (SPEC §13.5): promedio de los
/// puntajes por indicador clínico. Bandas de <c>|pct_change|</c> con dirección:
///
/// <list type="table">
/// <item><term>&gt;= 15% favorable</term><description>100</description></item>
/// <item><term>&gt;= 10% favorable</term><description>90</description></item>
/// <item><term>&gt;= 5% favorable</term><description>75</description></item>
/// <item><term>&gt;= 1% favorable</term><description>60</description></item>
/// <item><term>&lt; 1% (estable)</term><description>50</description></item>
/// <item><term>&gt;= 10% desfavorable</term><description>10</description></item>
/// <item><term>&gt;= 5% desfavorable</term><description>25</description></item>
/// <item><term>&gt;= 2% desfavorable</term><description>35</description></item>
/// <item><term>&lt; 2% desfavorable</term><description>45</description></item>
/// </list>
///
/// Sin indicadores (sin líneas base o sin mediciones en la semana) → puntaje 0
/// y detalle vacío (SPEC §13.5). El <c>detail</c> se arma con la forma JSONB
/// <c>{ metricCode: { baseline, current, unit, delta, delta_pct, favorable,
/// score } }</c> (§13.5).
/// </summary>
public sealed class TransformationScoreCalculator(ILogger<TransformationScoreCalculator> logger)
    : ITransformationScoreCalculator
{
    private const int MinScore = 0;
    private const int MaxScore = 100;

    public TransformationScoreResult Calculate(
        IReadOnlyList<TransformationIndicator> indicators,
        ScoreCalculationContext context)
    {
        var detail = new Dictionary<string, TransformationIndicatorScore>(indicators.Count);

        foreach (var indicator in indicators)
        {
            detail[indicator.MetricCode] = ScorePerIndicator(indicator);
        }

        var score = detail.Count == 0
            ? MinScore
            : Math.Clamp(
                (int)Math.Round(detail.Values.Average(d => d.Score), MidpointRounding.AwayFromZero),
                MinScore, MaxScore);

        // Log estructurado sin PHI (T-41): solo ids, semana, puntaje y el
        // número de indicadores (nunca valores clínicos, SPEC §13.6).
        logger.LogInformation(
            "Program.ScoreComputed: tipo=Transformation patient={PatientId} period={PeriodStart}..{PeriodEnd} " +
            "score={Score} indicators={IndicatorCount} trigger={Trigger}",
            context.PatientId, context.PeriodStart, context.PeriodEnd, score,
            detail.Count, context.Trigger);

        return new TransformationScoreResult(score, detail);
    }

    private static TransformationIndicatorScore ScorePerIndicator(TransformationIndicator indicator)
    {
        var delta = indicator.Current - indicator.Baseline;
        // baseline > 0 siempre (validación de la línea base: value > 0, AC-22).
        var deltaPct = indicator.Baseline == 0m ? 0m : delta / indicator.Baseline * 100m;
        var magnitude = Math.Abs(deltaPct);
        // favorable = cambio en la dirección de la métrica (SPEC §13.5).
        var favorable = deltaPct * (int)indicator.FavorableDirection > 0m;

        var score = ScoreForBand(magnitude, favorable);

        return new TransformationIndicatorScore(
            indicator.MetricCode,
            indicator.Baseline,
            indicator.Current,
            indicator.Unit,
            delta,
            Math.Round(deltaPct, 2, MidpointRounding.AwayFromZero),
            favorable,
            score);
    }

    private static int ScoreForBand(decimal magnitude, bool favorable)
    {
        // < 1% (estable): banda neutral, aplica a cualquier dirección (SPEC §13.5).
        if (magnitude < 1m)
        {
            return 50;
        }

        if (favorable)
        {
            if (magnitude >= 15m) return 100;
            if (magnitude >= 10m) return 90;
            if (magnitude >= 5m) return 75;
            return 60; // >= 1% favorable
        }

        if (magnitude >= 10m) return 10;
        if (magnitude >= 5m) return 25;
        if (magnitude >= 2m) return 35;
        return 45; // [1%, 2%) desfavorable
    }
}