using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Tablero clínico por paciente del módulo Pacientes: cinco señales que la
/// plataforma ya produce (riesgo de la última evaluación, alertas activas,
/// última evaluación, próxima evaluación pendiente y estado de seguimiento).
/// Paginado y filtrable; el alcance (clínica activa + propio vs global) lo
/// resuelve el backend, nunca el cliente. Sin caché: es una vista viva.
/// </summary>
public record GetClinicalBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Risk = null,
    bool? HasAlerts = null,
    string? FollowUp = null,
    Guid? ClinicId = null,
    Guid? OwnProfessionalId = null
) : IRequest<PaginatedClinicalBoardResult>;

/// <summary>Filtros de riesgo admitidos por la API (bucket del frontend).</summary>
public static class ClinicalBoardFilters
{
    public const string RiskHigh = "high";
    public const string RiskModerate = "moderate";
    public const string RiskLow = "low";

    public const string FollowUpOnTrack = "al-dia";
    public const string FollowUpOverdue = "vencido";
    public const string FollowUpUnassigned = "sin-asignacion";

    public static readonly IReadOnlySet<string> Risks = new HashSet<string>(
        [RiskHigh, RiskModerate, RiskLow],
        StringComparer.OrdinalIgnoreCase
    );

    public static readonly IReadOnlySet<string> FollowUps = new HashSet<string>(
        [FollowUpOnTrack, FollowUpOverdue, FollowUpUnassigned],
        StringComparer.OrdinalIgnoreCase
    );
}

/// <summary>Estados de seguimiento calculados (vocabulario del contrato).</summary>
public static class ClinicalBoardFollowUp
{
    public const string OnTrack = "al-dia";
    public const string Overdue = "vencido";
    public const string Unassigned = "sin-asignacion";
}

public sealed class GetClinicalBoardQueryHandler(IPatientDashboardRepository repository)
    : IRequestHandler<GetClinicalBoardQuery, PaginatedClinicalBoardResult>
{
    public async Task<PaginatedClinicalBoardResult> Handle(
        GetClinicalBoardQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var risk = ClinicalBoardFilters.Risks.Contains(request.Risk ?? string.Empty)
            ? request.Risk!.Trim().ToLowerInvariant()
            : null;
        var followUp = ClinicalBoardFilters.FollowUps.Contains(request.FollowUp ?? string.Empty)
            ? request.FollowUp!.Trim().ToLowerInvariant()
            : null;

        var now = DateTime.UtcNow;

        var (items, total) = await repository.GetClinicalBoardAsync(
            page,
            pageSize,
            search,
            risk,
            request.HasAlerts,
            followUp,
            request.ClinicId,
            request.OwnProfessionalId,
            now,
            ct
        );

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedClinicalBoardResult(items, total, page, pageSize, totalPages);
    }
}

/// <summary>
/// Fila del tablero clínico: identificación del paciente + cinco señales.
/// Los nulos son honestos: sin evaluaciones → <c>RiskLevel</c> null y fechas
/// null; sin asignaciones pendientes → <c>FollowUpState = sin-asignacion</c>.
/// </summary>
public record ClinicalBoardItemDto(
    Guid PatientId,
    string? MedicalRecordNumber,
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? RiskLevel,
    DateTime? LastEvaluationAt,
    string? LastEvaluationInstrument,
    int ActiveAlertCount,
    string? MaxAlertSeverity,
    DateTime? NextEvaluationDueAt,
    string FollowUpState
);

/// <summary>Resultado paginado del tablero clínico.</summary>
public record PaginatedClinicalBoardResult(
    IReadOnlyList<ClinicalBoardItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
