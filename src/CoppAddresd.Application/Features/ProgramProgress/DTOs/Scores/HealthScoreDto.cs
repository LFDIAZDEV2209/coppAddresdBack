using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Índice de Salud del contrato de <c>GET /program/scores</c> (SPEC §13.7.1):
/// puntaje actual, el persistido anterior (para la tendencia) y las 5
/// dimensiones. Los nombres JSON son snake_case exactamente como el contrato.
/// </summary>
public sealed record HealthScoreDto(
    [property: JsonPropertyName("current")] int Current,
    [property: JsonPropertyName("previous")] int? Previous,
    [property: JsonPropertyName("trend")] string Trend,
    [property: JsonPropertyName("dimensions")] HealthScoreDimensionsDto Dimensions);

/// <summary>Las 5 dimensiones del Índice de Salud (0..100).</summary>
public sealed record HealthScoreDimensionsDto(
    [property: JsonPropertyName("adherence")] int Adherence,
    [property: JsonPropertyName("clinical")] int Clinical,
    [property: JsonPropertyName("nutrition")] int Nutrition,
    [property: JsonPropertyName("psychology")] int Psychology,
    [property: JsonPropertyName("exercise")] int Exercise);