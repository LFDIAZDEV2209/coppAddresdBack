using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Detalle de un indicador del Índice de Transformación (SPEC §13.5): línea
/// base, medición actual, unidad (símbolo), delta absoluto, delta porcentual
/// redondeado a 2 decimales, si el cambio es favorable y el puntaje de banda.
/// </summary>
public sealed record IndicatorDetailDto(
    [property: JsonPropertyName("baseline")] decimal Baseline,
    [property: JsonPropertyName("current")] decimal Current,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("delta")] decimal Delta,
    [property: JsonPropertyName("delta_pct")] decimal DeltaPct,
    [property: JsonPropertyName("favorable")] bool Favorable,
    [property: JsonPropertyName("score")] int Score);