using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Estado de un día del período para la dimensión de adherencia
/// (SPEC §13.4.1): perfecto (1.0), parcial (0.6), rescatado por congelamiento
/// (0.4) o perdido (0.0).
/// </summary>
public enum AdherenceDayStatus
{
    Perfect,
    Partial,
    Rescued,
    Missed,
}

/// <summary>
/// Calculador del Índice de Salud (SPEC §13.4): función pura sobre los datos
/// de ventana que el repositorio reúne. Bandas:
/// - <b>adherence</b> (§13.4.1): pesos por día del período (perfecto 1.0 /
///   parcial 0.6 / congelamiento 0.4 / perdido 0.0); sin check-ins → 0.
/// - <b>clinical</b> (§13.4.2): cambio porcentual vs línea base; sin bases o
///   sin mediciones → 50 (neutral, mismo default que el sistema de referencia).
/// - <b>nutrition</b> (§13.4.3): logros / totales de hábitos alimentarios;
///   sin logs → 0.
/// - <b>psychology</b> (§13.4.4): rescale de <c>mood ∈ [1,5]</c> → 0..100;
///   sin registros emocionales → 60 (moderado, default de referencia).
/// - <b>exercise</b> (§13.4.5): días con <c>ejercicio</c> / días del período;
///   sin completaciones → 0.
///
/// El ponderado aplica los pesos de <c>app.health_score_weights</c>; si la
/// suma no es 1.0000 se normaliza (defensa AC-19: la escritura de pesos ya
/// valida la suma antes de persistir).
/// </summary>
public sealed class HealthScoreCalculator(ILogger<HealthScoreCalculator> logger)
    : IHealthScoreCalculator
{
    private const int MinScore = 0;
    private const int MaxScore = 100;

    public HealthScoreResult Calculate(HealthScoreInput input, ScoreCalculationContext context)
    {
        var adherence = CalculateAdherence(input);
        var clinical = CalculateClinical(input);
        var nutrition = CalculateNutrition(input);
        var psychology = CalculatePsychology(input);
        var exercise = CalculateExercise(input);

        var total = CalculateWeightedTotal(
            input.Weights, adherence, clinical, nutrition, psychology, exercise);

        // Log estructurado sin PHI (T-41): solo ids, período, puntaje y
        // dimensiones. Nunca mood_score/barriers/notas (SPEC §13.6).
        logger.LogInformation(
            "Program.ScoreComputed: tipo=Health patient={PatientId} period={PeriodStart}..{PeriodEnd} " +
            "score={Score} dimensions={Adherence},{Clinical},{Nutrition},{Psychology},{Exercise} trigger={Trigger}",
            context.PatientId, context.PeriodStart, context.PeriodEnd, total,
            adherence, clinical, nutrition, psychology, exercise, context.Trigger);

        return new HealthScoreResult(adherence, clinical, nutrition, psychology, exercise, total);
    }

    // ---------------------------------------------------------------- 13.4.1

    private static int CalculateAdherence(HealthScoreInput input)
    {
        // Sin check-ins en el período → adherencia 0 (SPEC §13.4.1).
        if (input.AdherenceDays.Count == 0)
        {
            return MinScore;
        }

        var dayCount = Math.Max(input.DayCount, input.AdherenceDays.Count);
        var sum = input.AdherenceDays.Sum(day => day switch
        {
            AdherenceDayStatus.Perfect => 1.0m,
            AdherenceDayStatus.Partial => 0.6m,
            AdherenceDayStatus.Rescued => 0.4m,
            AdherenceDayStatus.Missed => 0.0m,
            _ => 0.0m,
        });

        return RoundToScore(sum / dayCount * 100m);
    }

    // ---------------------------------------------------------------- 13.4.2

    private static int CalculateClinical(HealthScoreInput input)
    {
        // Sin líneas base O sin mediciones → 50 (neutral, SPEC §13.4.2).
        if (input.ClinicalIndicators.Count == 0)
        {
            return 50;
        }

        var scores = input.ClinicalIndicators.Select(ScorePerMetric).ToList();
        return RoundToScore((decimal)scores.Average());
    }

    private static int ScorePerMetric(ClinicalIndicator indicator)
    {
        // pct_change = (current - baseline) / baseline; baseline > 0 siempre
        // (validación de la línea base: value > 0, AC-22). El cambio es
        // favorable si su signo coincide con la dirección favorable de la
        // métrica (SPEC §13.4.2: favorable = pct_change * favorable_direction > 0).
        var pctChange = (indicator.Current - indicator.Baseline) / indicator.Baseline * 100m;
        var magnitude = Math.Abs(pctChange);
        var favorable = pctChange * (int)indicator.FavorableDirection > 0m;

        if (favorable && magnitude > 5m) return 100;
        if (favorable && magnitude > 1m) return 75;
        if (magnitude <= 1m) return 50;
        if (magnitude > 5m) return 10;
        return 25; // unfavorable && 1% < magnitude <= 5%
    }

    // ---------------------------------------------------------------- 13.4.3

    private static int CalculateNutrition(HealthScoreInput input)
    {
        if (input.Nutrition is not { } nutrition || nutrition.Total <= 0)
        {
            return MinScore;
        }

        var achieved = Math.Clamp(nutrition.Achieved, 0, nutrition.Total);
        return RoundToScore(achieved / (decimal)nutrition.Total * 100m);
    }

    // ---------------------------------------------------------------- 13.4.4

    private static int CalculatePsychology(HealthScoreInput input)
    {
        if (input.MoodScores.Count == 0)
        {
            return 60; // default moderado sin registros emocionales (§13.4.4)
        }

        var rescaled = input.MoodScores
            .Select(mood => (mood - 1) / 4m * 100m)
            .ToList();
        return RoundToScore(rescaled.Average());
    }

    // ---------------------------------------------------------------- 13.4.5

    private static int CalculateExercise(HealthScoreInput input)
    {
        if (input.ExerciseDaysWithCompletion <= 0)
        {
            return MinScore;
        }

        var dayCount = Math.Max(input.DayCount, input.ExerciseDaysWithCompletion);
        return RoundToScore(input.ExerciseDaysWithCompletion / (decimal)dayCount * 100m);
    }

    // ---------------------------------------------------------------- 13.4 (ponderado)

    private static int CalculateWeightedTotal(
        IReadOnlyList<ScoreWeight> weights,
        int adherence,
        int clinical,
        int nutrition,
        int psychology,
        int exercise)
    {
        var byDimension = weights.ToDictionary(w => w.Dimension, w => w.Weight);
        var sum = byDimension.Values.Sum();

        // Normalización defensiva (AC-19): si la suma no es 1.0000 (p. ej. peso
        // 0), se re-escala para que el ponderado siga viviendo en 0..100.
        decimal Scale(ScoreDimension dimension) =>
            byDimension.TryGetValue(dimension, out var weight)
                ? (sum == 0m ? 0m : weight / sum)
                : 0m;

        var total =
              adherence * Scale(ScoreDimension.adherence)
            + clinical * Scale(ScoreDimension.clinical)
            + nutrition * Scale(ScoreDimension.nutrition)
            + psychology * Scale(ScoreDimension.psychology)
            + exercise * Scale(ScoreDimension.exercise);

        return RoundToScore(total);
    }

    private static int RoundToScore(decimal value)
        => Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), MinScore, MaxScore);
}