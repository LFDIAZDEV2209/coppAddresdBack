using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Índice de Transformación del contrato de <c>GET /program/scores</c>
/// (SPEC §13.7.1): puntaje actual, el persistido anterior, la tendencia, la
/// semana del programa y el detalle por métrica (clave = código de la métrica).
/// </summary>
public sealed record TransformationScoreDto(
    [property: JsonPropertyName("current")] int Current,
    [property: JsonPropertyName("previous")] int? Previous,
    [property: JsonPropertyName("trend")] string Trend,
    [property: JsonPropertyName("week")] int Week,
    [property: JsonPropertyName("detail")] IReadOnlyDictionary<string, IndicatorDetailDto> Detail);