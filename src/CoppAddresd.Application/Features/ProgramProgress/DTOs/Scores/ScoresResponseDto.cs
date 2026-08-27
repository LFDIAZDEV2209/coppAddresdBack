using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Respuesta de <c>GET /program/scores</c> y <c>POST /program/scores/calculate</c>
/// (SPEC §13.7.1): el Índice de Salud + el Índice de Transformación del
/// paciente. El móvil lo usa en la pestaña Evolución (T-40).
/// </summary>
public sealed record ScoresResponseDto(
    [property: JsonPropertyName("health_score")] HealthScoreDto HealthScore,
    [property: JsonPropertyName("transformation_score")] TransformationScoreDto TransformationScore);