using System.Text.Json.Serialization;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;

/// <summary>
/// Fila de una intervención (SPEC §22, D): shape del
/// <c>GET /api/v1/program/interventions</c> y de la cola clínica
/// <c>GET /api/v1/program/interventions/open</c>. Refleja la fila de
/// <c>app.interventions</c> (tipo, título, estado, severidad, asignación,
/// timestamps del ciclo de vida, XP acumulada).
/// </summary>
public sealed record InterventionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("patientId")] Guid PatientId,
    [property: JsonPropertyName("weaknessId")] Guid? WeaknessId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("assignedTo")] Guid? AssignedTo,
    [property: JsonPropertyName("recommendedAt")] DateTime? RecommendedAt,
    [property: JsonPropertyName("acceptedAt")] DateTime? AcceptedAt,
    [property: JsonPropertyName("completedAt")] DateTime? CompletedAt,
    [property: JsonPropertyName("patientAction")] string? PatientAction,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("xpAwardedTotal")] int XpAwardedTotal,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt);

/// <summary>
/// Resultado paginado de una lista de intervenciones (SPEC §22, D): la lista de
/// filas ordenada por <c>created_at</c>, el total de filas y la paginación.
/// </summary>
public sealed record PaginatedInterventionsResult(
    [property: JsonPropertyName("data")] IReadOnlyList<InterventionDto> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalPages")] int TotalPages);

/// <summary>
/// Payload de <c>POST /api/v1/program/interventions/{id}/status</c> (SPEC §22, D):
/// actualización de estado por el clínico. El resultado es requerido solo para
/// el estado <c>completed</c>.
/// </summary>
public sealed record UpdateInterventionStatusRequest(
    InterventionStatus Status,
    string? Result = null,
    Guid? AssignedTo = null);
