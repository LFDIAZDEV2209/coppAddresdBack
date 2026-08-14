using CoppAddresd.Application.Common;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>DTO de monitoreo de ejecución de agente (proxy del AI Service).</summary>
public record AgentExecutionSummaryDto(
    string Id,
    string? ThreadId,
    string AgentTypeId,
    string? VersionId,
    string? UserId,
    string Status,
    int? LatencyMs,
    int? TokensIn,
    int? TokensOut,
    string? Model,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    int? FeedbackRating,
    double? BestEvaluation);

/// <summary>DTO de detalle de ejecución (las 12 preguntas del monitoreo).</summary>
public record AgentExecutionDetailDto(
    string Id,
    string? ThreadId,
    string AgentTypeId,
    string? VersionId,
    string? UserId,
    string Status,
    int? LatencyMs,
    int? TokensIn,
    int? TokensOut,
    string? Model,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndedAt,
    int? FeedbackRating,
    double? BestEvaluation,
    string? FeedbackComment,
    IReadOnlyDictionary<string, object>? Input,
    IReadOnlyDictionary<string, object>? Output,
    IReadOnlyList<AgentEvaluationDto> Evaluations,
    IReadOnlyList<AgentExperienceLiteDto> Experiences);

/// <summary>Evaluación programática de una ejecución.</summary>
public record AgentEvaluationDto(
    string Evaluator,
    string? Metric,
    double? Score,
    string? Details);

/// <summary>Experiencia generada por el agente (vista para el admin).</summary>
public record AgentExperienceLiteDto(
    string Trigger,
    string Response,
    string? Outcome,
    int Recurrence,
    double? Rating);

/// <summary>Resultado paginado de ejecuciones.</summary>
public record AgentExecutionsListDto(
    int Total,
    IReadOnlyList<AgentExecutionSummaryDto> Items);

/// <summary>Payload del AI Service en snake_case (contrato interno).</summary>
public sealed class AgentExecutionQueryOptions
{
    public string? AgentTypeId { get; init; }
    public string? UserId { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public int Limit { get; init; } = 50;
    public int Offset { get; init; }

    public string ToQueryString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(AgentTypeId)) parts.Add($"agent_type_id={Uri.EscapeDataString(AgentTypeId)}");
        if (!string.IsNullOrWhiteSpace(UserId)) parts.Add($"user_id={Uri.EscapeDataString(UserId)}");
        if (!string.IsNullOrWhiteSpace(Status)) parts.Add($"status={Uri.EscapeDataString(Status)}");
        if (FromDate is not null) parts.Add($"from_date={FromDate.Value:O}");
        if (ToDate is not null) parts.Add($"to_date={ToDate.Value:O}");
        parts.Add($"limit={Limit}");
        parts.Add($"offset={Offset}");
        return string.Join("&", parts);
    }
}

/// <summary>Configuración del endpoint de ejecuciones del AI Service.</summary>
public sealed class AgentExecutionsEndpointSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiPrefix { get; set; } = "/api/v1";
    public string ExecutionsEndpoint => $"{ApiPrefix}/admin/executions";
}
