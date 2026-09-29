using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Sos;

// ===================== Consultas (paciente dueño / staff con scope) =====================
// El IDOR lo resuelve el handler: el paciente solo ve SUS alertas (cualquier
// id de terceros → 404 no revelador). El staff consulta con su propio scope
// (el controller valida permiso + audiencia).

/// <summary>
/// Listado paginado de alertas para el staff ERP (bandeja SOS del borrador
/// del front). El alcance viene resuelto por el caller: <c>PatientIds</c>
/// <c>null</c> = bypass (roles de administración org/clínica); lista = filtro
/// IN por pacientes alcanzables (lista vacía = denegar, 0 filas). Paginación
/// estándar del repo (page/pageSize, clamp 1-100).
/// </summary>
public record ListSosAlertsForStaffQuery(
    IReadOnlyList<Guid>? PatientIds,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<SosAlertsPage>;

public sealed class ListSosAlertsForStaffHandler(ISosAlertRepository repository)
    : IRequestHandler<ListSosAlertsForStaffQuery, SosAlertsPage>
{
    public async Task<SosAlertsPage> Handle(
        ListSosAlertsForStaffQuery request,
        CancellationToken ct
    )
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var (items, total) = await repository.ListForStaffAsync(
            request.PatientIds,
            request.Status,
            page,
            pageSize,
            ct
        );

        return new SosAlertsPage(
            items,
            total,
            page,
            pageSize,
            Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))
        );
    }
}

/// <summary>Alerta activa del paciente autenticado (aud=app) o null.</summary>
public record GetActiveSosAlertQuery(Guid PatientId) : IRequest<SosAlertDto?>;

public sealed class GetActiveSosAlertHandler(ISosAlertRepository repository)
    : IRequestHandler<GetActiveSosAlertQuery, SosAlertDto?>
{
    public async Task<SosAlertDto?> Handle(GetActiveSosAlertQuery request, CancellationToken ct)
    {
        var alert = await repository.GetActiveByPatientAsync(request.PatientId, ct);
        return alert is null ? null : SosAlertDto.FromEntity(alert);
    }
}

/// <summary>
/// Detalle de una alerta para su dueño (paciente: <c>sub == alert.PatientId</c>).
/// Cualquier id ajeno → null (404 no revelador en el controller).
/// </summary>
public record GetSosAlertQuery(Guid PatientId, Guid AlertId) : IRequest<SosAlertDto?>;

public sealed class GetSosAlertHandler(ISosAlertRepository repository)
    : IRequestHandler<GetSosAlertQuery, SosAlertDto?>
{
    public async Task<SosAlertDto?> Handle(GetSosAlertQuery request, CancellationToken ct)
    {
        var alert = await repository.GetByIdAsync(request.AlertId, ct);
        if (alert is null || alert.PatientId != request.PatientId)
        {
            return null;
        }

        return SosAlertDto.FromEntity(alert);
    }
}

/// <summary>
/// Detalle de una alerta para staff (aud=erp, permiso <c>Sos.Alerts.Manage</c>).
/// Devuelve <c>null</c> si no existe y <c>Forbidden</c> si el scope clínico
/// no cubre al paciente (ambos sin revelar datos, REQ-SOS-05).
/// </summary>
public record GetSosAlertForStaffQuery(Guid AlertId, SosStaffActor Staff)
    : IRequest<SosStaffAlertResult>;

public sealed record SosStaffAlertResult(SosTransitionOutcome Outcome, SosAlertDto? Alert);

public sealed class GetSosAlertForStaffHandler(ISosAlertRepository repository)
    : IRequestHandler<GetSosAlertForStaffQuery, SosStaffAlertResult>
{
    public async Task<SosStaffAlertResult> Handle(
        GetSosAlertForStaffQuery request,
        CancellationToken ct
    )
    {
        var alert = await repository.GetByIdAsync(request.AlertId, ct);
        if (alert is null)
        {
            return new SosStaffAlertResult(SosTransitionOutcome.NotFound, null);
        }

        var scoped =
            request.Staff.BypassScope
            || await repository.IsStaffScopedToPatientAsync(
                alert.PatientId,
                request.Staff.ProfessionalId,
                request.Staff.ActiveClinicId,
                request.Staff.ActiveOrganizationId,
                ct
            );

        return scoped
            ? new SosStaffAlertResult(SosTransitionOutcome.Transited, SosAlertDto.FromEntity(alert))
            : new SosStaffAlertResult(SosTransitionOutcome.Forbidden, null);
    }
}
