using System.Text.Json.Serialization;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;

/// <summary>
/// Fila de la lista de debilidades del paciente (SPEC §21, D): shape del
/// <c>GET /api/v1/program/weaknesses</c> y de la cola clínica
/// <c>GET /api/v1/program/weaknesses/open</c>. Refleja la fila de
/// <c>app.weaknesses</c> (código de regla, categoría, severidad, indicador y
/// ciclo de vida).
/// </summary>
public sealed record WeaknessDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("patientId")] Guid PatientId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("detectedAt")] DateTime DetectedAt,
    [property: JsonPropertyName("metricId")] Guid? MetricId,
    [property: JsonPropertyName("indicatorValue")] decimal? IndicatorValue,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("assignedTo")] Guid? AssignedTo,
    [property: JsonPropertyName("resolvedAt")] DateTime? ResolvedAt,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt);

/// <summary>
/// Resultado paginado de una lista de debilidades (SPEC §21, D): la lista de
/// filas ordenada por <c>detected_at</c> (descendente en la vista del paciente,
/// ascendente FIFO en la cola clínica), el total de filas de la consulta y la
/// paginación.
/// </summary>
public sealed record PaginatedWeaknessesResult(
    [property: JsonPropertyName("data")] IReadOnlyList<WeaknessDto> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalPages")] int TotalPages);

/// <summary>
/// Payload de <c>POST /api/v1/program/weaknesses/{{id}}/status</c> (SPEC §21, D):
/// transición de estado del ciclo de vida. El estado <c>open</c> NO es válido
/// por esta vía (una fila recién detectada ya nace abierta; el clínico la
/// reconoce, la interviene, la resuelve o la descarta).
/// </summary>
public sealed record UpdateWeaknessStatusRequest(WeaknessStatus Status);