using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.ActivityLog;

/// <summary>
/// Fila de la bitácora de actividad del módulo programa (ERP): una entrada del
/// log de auditoría trigger-based (<c>audit.activity_logs</c>) filtrada a las
/// tablas <c>app.*</c> del módulo. Sin <c>old_data</c>/<c>new_data</c>: el
/// detalle crudo puede contener PHI y no se expone por la superficie admin
/// (convención §8.5 del módulo). Naming camelCase (convención de los DTOs ERP).
/// </summary>
public sealed record ActivityLogEntryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("tableName")] string TableName,
    [property: JsonPropertyName("recordId")] string RecordId,
    [property: JsonPropertyName("actorType")] string ActorType,
    [property: JsonPropertyName("actorEmail")] string? ActorEmail,
    [property: JsonPropertyName("actorRole")] string? ActorRole);

/// <summary>
/// Respuesta paginada de la bitácora de actividad: filas ordenadas por
/// <c>occurredAt</c> descendente (la más reciente primero).
/// </summary>
public sealed record PaginatedActivityLogResult(
    [property: JsonPropertyName("data")] IReadOnlyList<ActivityLogEntryDto> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalPages")] int TotalPages);
