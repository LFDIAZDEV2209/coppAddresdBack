using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Tablero clínico por paciente del módulo Pacientes: las señales que la
/// plataforma ya produce (riesgo de la última evaluación, alertas activas,
/// última evaluación, próxima evaluación pendiente y estado de seguimiento)
/// más los datos de directorio útiles (diagnóstico principal, ubicación,
/// aseguradora, profesionales y estado del paciente). Incluye un resumen por
/// buckets para las tarjetas del tab. Paginado y filtrable; el alcance
/// (clínica activa + propio vs global) lo resuelve el backend, nunca el
/// cliente. Sin caché: es una vista viva.
/// </summary>
public record GetClinicalBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Risk = null,
    bool? HasAlerts = null,
    string? FollowUp = null,
    Guid? ClinicId = null,
    Guid? OwnProfessionalId = null,
    string? Status = null,
    Guid? InsurerId = null,
    string? StateCode = null
) : IRequest<PaginatedClinicalBoardResult>;

/// <summary>Filtros de riesgo admitidos por la API (bucket del frontend).</summary>
public static class ClinicalBoardFilters
{
    public const string RiskHigh = "high";
    public const string RiskModerate = "moderate";
    public const string RiskLow = "low";

    /// <summary>Severidad explícitamente crítica (subconjunto de <c>high</c>).</summary>
    public const string RiskCritical = "critical";

    public const string FollowUpOnTrack = "al-dia";
    public const string FollowUpOverdue = "vencido";
    public const string FollowUpUnassigned = "sin-asignacion";

    public static readonly IReadOnlySet<string> Risks = new HashSet<string>(
        [RiskHigh, RiskModerate, RiskLow, RiskCritical],
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
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
        var stateCode = string.IsNullOrWhiteSpace(request.StateCode)
            ? null
            : request.StateCode.Trim().ToUpperInvariant();

        var now = DateTime.UtcNow;

        var (items, total, summary) = await repository.GetClinicalBoardAsync(
            page,
            pageSize,
            search,
            risk,
            request.HasAlerts,
            followUp,
            status,
            request.InsurerId,
            stateCode,
            request.ClinicId,
            request.OwnProfessionalId,
            now,
            ct
        );

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedClinicalBoardResult(items, summary, total, page, pageSize, totalPages);
    }
}

/// <summary>
/// Fila del tablero clínico: identificación del paciente + datos de directorio
/// útiles (diagnóstico principal, ubicación, aseguradora, profesionales y
/// estado) + señales clínicas. Los nulos son honestos: sin evaluaciones →
/// <c>RiskLevel</c> null y fechas null; sin asignaciones pendientes →
/// <c>FollowUpState = sin-asignacion</c>; <c>ProfessionalNames</c> solo viaja
/// con alcance global (con alcance propio todos son del mismo profesional).
/// </summary>
public record ClinicalBoardItemDto(
    Guid PatientId,
    string? MedicalRecordNumber,
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? PrimaryDiagnosisCode,
    string? PrimaryDiagnosisDescription,
    string? StateCode,
    string? StateName,
    string? InsurerName,
    IReadOnlyList<string> ProfessionalNames,
    string Status,
    string? RiskLevel,
    DateTime? LastEvaluationAt,
    string? LastEvaluationInstrument,
    int ActiveAlertCount,
    string? MaxAlertSeverity,
    DateTime? NextEvaluationDueAt,
    string FollowUpState
);

/// <summary>
/// Resumen del tablero para las tarjetas del tab, sobre el alcance y los
/// filtros no clínicos (búsqueda, estado, aseguradora, estado geográfico):
/// los filtros clínicos (riesgo/alertas/seguimiento) no lo acotan para que las
/// tarjetas funcionen como puntos de entrada. Los buckets de riesgo replican
/// exactamente las condiciones de los filtros (high incluye critical).
/// </summary>
public record ClinicalBoardSummaryDto(
    int Total,
    int WithoutEvaluation,
    int RiskHigh,
    int RiskModerate,
    int RiskLow,
    int WithActiveAlerts,
    int FollowUpOnTrack,
    int FollowUpOverdue,
    int FollowUpUnassigned
);

/// <summary>Resultado paginado del tablero clínico (+ resumen de tarjetas).</summary>
public record PaginatedClinicalBoardResult(
    IReadOnlyList<ClinicalBoardItemDto> Data,
    ClinicalBoardSummaryDto Summary,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
