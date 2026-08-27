using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Indicador del Índice de Transformación (SPEC §13.5): línea base y medición
/// más reciente de la semana del programa para la misma métrica, con la unidad
/// (símbolo) y la dirección favorable de la línea base. El repositorio entrega
/// SOLO pares (base, medición) que existen en la ventana de la semana.
/// </summary>
public sealed record TransformationIndicator(
    string MetricCode,
    decimal Baseline,
    decimal Current,
    string Unit,
    FavorableDirection FavorableDirection);

/// <summary>Puntaje de un indicador con el detalle que se persiste en el <c>jsonb</c>.</summary>
public sealed record TransformationIndicatorScore(
    string MetricCode,
    decimal Baseline,
    decimal Current,
    string Unit,
    decimal Delta,
    decimal DeltaPct,
    bool Favorable,
    int Score);

/// <summary>
/// Resultado del Índice de Transformación: promedio redondeado y el detalle
/// por métrica (SPEC §13.5). Sin indicadores → <c>Score = 0</c> y
/// <c>Detail</c> vacío.
/// </summary>
public sealed record TransformationScoreResult(
    int Score,
    IReadOnlyDictionary<string, TransformationIndicatorScore> Detail);

/// <summary>
/// Calculador del Índice de Transformación (SPEC §13.5): función pura que
/// aplica las bandas de cambio porcentual por indicador sobre los datos de la
/// semana que el repositorio entrega. Sin I/O; único efecto observable: el log
/// estructurado <c>Program.ScoreComputed</c> (T-41, sin PHI).
/// </summary>
public interface ITransformationScoreCalculator
{
    /// <summary>Calcula el puntaje promedio y el detalle por métrica.</summary>
    TransformationScoreResult Calculate(
        IReadOnlyList<TransformationIndicator> indicators,
        ScoreCalculationContext context);
}