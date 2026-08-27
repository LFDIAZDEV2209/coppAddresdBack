using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Contexto de un cálculo de puntaje para el log estructurado (T-41): identidad
/// del paciente, período y disparador. El log nunca transporta PHI (sin
/// mood_score, sin barriers, sin notas, SPEC §13.6).
/// </summary>
public enum ScoreTrigger
{
    /// <summary>Cálculo disparado por <c>GET /program/scores</c> (on-read, SPEC §13.3).</summary>
    OnRead,

    /// <summary>Recálculo forzado por <c>POST /program/scores/calculate</c> (clínico, SPEC §13.6).</summary>
    ManualRecompute,
}

/// <summary>
/// Contexto de un cálculo de puntaje: paciente, período local y disparador.
/// El período SIEMPRE llega en fecha local del paciente (SPEC §13.2); la
/// conversión UTC → local la resuelve el repositorio, nunca el calculador.
/// </summary>
public sealed record ScoreCalculationContext(
    Guid PatientId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ScoreTrigger Trigger);

/// <summary>Peso de una dimensión del Índice de Salud (SPEC §13.1.1).</summary>
public sealed record ScoreWeight(ScoreDimension Dimension, decimal Weight);

/// <summary>
/// Indicador clínico para el Índice de Salud (SPEC §13.4.2): línea base y
/// medición más reciente del período para la misma métrica, con la dirección
/// favorable de la línea base (<c>FavorableDirection</c>). El repositorio
/// entrega SOLO pares (base, medición) que existen en el período.
/// </summary>
public sealed record ClinicalIndicator(
    string MetricCode,
    decimal Baseline,
    decimal Current,
    FavorableDirection FavorableDirection);

/// <summary>Log de hábitos de alimentación del período (SPEC §13.4.3).</summary>
public sealed record NutritionLog(int Achieved, int Total);

/// <summary>
/// Entrada completa del Índice de Salud: los datos de ventana que el
/// repositorio reunió con queries set-based. El calculador es una función pura
/// sin I/O (determinista y testeable en unit).
/// </summary>
public sealed record HealthScoreInput(
    IReadOnlyList<ScoreWeight> Weights,
    IReadOnlyList<AdherenceDayStatus> AdherenceDays,
    IReadOnlyList<ClinicalIndicator> ClinicalIndicators,
    NutritionLog? Nutrition,
    IReadOnlyList<short> MoodScores,
    int ExerciseDaysWithCompletion,
    int DayCount);

/// <summary>Resultado del Índice de Salud (dimensiones 0..100 + ponderado).</summary>
public sealed record HealthScoreResult(
    int Adherence,
    int Clinical,
    int Nutrition,
    int Psychology,
    int Exercise,
    int Total);

/// <summary>
/// Calculador del Índice de Salud (SPEC §13.4): función pura que aplica las
/// bandas de cada dimensión sobre los datos de ventana que el repositorio
/// entrega. Sin I/O; el único efecto observable es el log estructurado
/// <c>Program.ScoreComputed</c> (T-41, sin PHI).
/// </summary>
public interface IHealthScoreCalculator
{
    /// <summary>
    /// Calcula las 5 dimensiones y el ponderado según los pesos de
    /// <c>app.health_score_weights</c>. Si los pesos no suman 1.0000 se
    /// normalizan (defensa AC-19: la escritura de pesos ya los valida).
    /// </summary>
    HealthScoreResult Calculate(HealthScoreInput input, ScoreCalculationContext context);
}