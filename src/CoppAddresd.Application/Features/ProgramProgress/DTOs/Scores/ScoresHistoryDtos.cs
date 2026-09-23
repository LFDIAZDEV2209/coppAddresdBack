using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Respuesta de <c>GET /program/me/scores-history</c> (historial del paciente
/// autenticado para la pestaña Evolución del móvil): serie semanal
/// ASCENDENTE de puntajes, SOLO con semanas que tienen al menos una fila
/// persistida (nunca semanas vacías). Nombres wire camelCase, convención
/// <c>me/*</c> (espejo de LeagueDtos).
/// </summary>
public sealed record ScoresHistoryResponseDto(
    [property: JsonPropertyName("points")] IReadOnlyList<ScoresHistoryPointDto> Points
);

/// <summary>
/// Punto de la serie: una semana del programa. <c>WeekNumber</c> sale de
/// <c>transformation_scores.week_number</c> o, para filas de salud sin
/// transformación, de la semana de la inscripción que contiene su
/// <c>period_end</c>. <c>PeriodStart</c>/<c>PeriodEnd</c> son el rango de la
/// semana del programa (punto anclado por transformación) o el período
/// PERSISTIDO de la fila (punto solo de salud). <c>HealthScore</c>/
/// <c>TransformationScore</c> son null cuando esa tabla no tiene fila para la
/// semana; <c>HealthPrevious</c> es el <c>score_previous</c> persistido
/// (puede ser null).
/// </summary>
public sealed record ScoresHistoryPointDto(
    [property: JsonPropertyName("weekNumber")] int WeekNumber,
    [property: JsonPropertyName("periodStart")] DateOnly PeriodStart,
    [property: JsonPropertyName("periodEnd")] DateOnly PeriodEnd,
    [property: JsonPropertyName("healthScore")] int? HealthScore,
    [property: JsonPropertyName("healthPrevious")] int? HealthPrevious,
    [property: JsonPropertyName("transformationScore")] int? TransformationScore,
    [property: JsonPropertyName("dimensions")] HealthScoreDimensionsDto? Dimensions = null
);

// ===========================================================================
// Resultados del repositorio (NO cacheados; espejo de LeagueContext en
// LeagueDtos.cs): contexto de la inscripción activa + filas persistidas.
// ===========================================================================

/// <summary>
/// Contexto del historial (resultado del repositorio, nunca cacheado):
/// semanas materializadas de la inscripción ACTIVA + filas persistidas de
/// ambas tablas. Los tres listados llegan ordenados ASC por su eje
/// (weekNumber / periodEnd).
/// </summary>
public sealed record ScoresHistoryContext(
    IReadOnlyList<ScoreWeekRow> Weeks,
    IReadOnlyList<HealthScoreHistoryRow> HealthRows,
    IReadOnlyList<TransformationScoreHistoryRow> TransformationRows
);

/// <summary>Semana materializada de la inscripción (<c>ProgramWeek</c>): número + rango local.</summary>
public sealed record ScoreWeekRow(
    int WeekNumber,
    DateOnly WeekStartDateLocal,
    DateOnly WeekEndDateLocal
);

/// <summary>Fila persistida de <c>app.health_scores</c> (proyección mínima para la serie + 5 dimensiones).</summary>
public sealed record HealthScoreHistoryRow(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Score,
    int? ScorePrevious,
    int ScoreAdherence,
    int ScoreClinical,
    int ScoreNutrition,
    int ScorePsychology,
    int ScoreExercise
);

/// <summary>Fila persistida de <c>app.transformation_scores</c> (proyección mínima para la serie).</summary>
public sealed record TransformationScoreHistoryRow(int WeekNumber, int Score);
