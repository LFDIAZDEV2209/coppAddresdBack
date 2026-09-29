using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Sos;

// ===================== Ciclo de vida: transiciones terminales =====================
// Activa → Atendida (staff ERP) / Activa → Cancelada (paciente dueño).
// Ambas transiciones son TERMINALES: un intento posterior responde 409
// (REQ-SOS-05). El anti-IDOR lo resuelve el handler: alerta ajena → 404 no
// revelador (nunca se confirma la existencia de alertas de terceros).

/// <summary>
/// Cancelación por el paciente dueño (aud=app). El patientId viene derivado
/// del JWT; si la alerta pertenece a otro paciente se responde 404 sin
/// revelar su existencia. <paramref name="CancelledByUserId"/> es el usuario
/// de <c>auth.users</c> del JWT (actor de auditoría).
/// </summary>
public record CancelSosAlertCommand(Guid PatientId, Guid AlertId, Guid CancelledByUserId)
    : IRequest<SosTransitionResult>;

public sealed class CancelSosAlertHandler(
    ISosAlertRepository repository,
    ILogger<CancelSosAlertHandler> logger
) : IRequestHandler<CancelSosAlertCommand, SosTransitionResult>
{
    public async Task<SosTransitionResult> Handle(
        CancelSosAlertCommand request,
        CancellationToken ct
    )
    {
        var alert = await repository.GetByIdAsync(request.AlertId, ct);
        if (alert is null || alert.PatientId != request.PatientId)
        {
            // 404 no revelador: existe pero es de otro paciente, o no existe.
            return new SosTransitionResult(SosTransitionOutcome.NotFound, null);
        }

        if (alert.Status != SosAlertStatus.Activa)
        {
            return new SosTransitionResult(
                SosTransitionOutcome.NotActive,
                SosAlertDto.FromEntity(alert)
            );
        }

        // Compare-and-set atómico: si otra transición (attend/cancel) ganó la
        // carrera, affected=0 → 409 con el estado terminal actual.
        var applied = await repository.CancelAsync(request.AlertId, request.CancelledByUserId, ct);
        if (!applied)
        {
            var current = await repository.GetByIdAsync(request.AlertId, ct);
            return new SosTransitionResult(
                SosTransitionOutcome.NotActive,
                current is null ? null : SosAlertDto.FromEntity(current)
            );
        }

        var fresh = await repository.GetByIdAsync(request.AlertId, ct);

        logger.LogInformation(
            "SOS cancelada por el paciente: alertId={AlertId}, patientId={PatientId}.",
            request.AlertId,
            request.PatientId
        );

        return new SosTransitionResult(
            SosTransitionOutcome.Transited,
            fresh is null ? null : SosAlertDto.FromEntity(fresh)
        );
    }
}

/// <summary>
/// Contexto de scope del staff ERP (aud=erp) resuelto por el controller a
/// partir del JWT y los headers de contexto activo (X-Clinic-Id /
/// X-Organization-Id). El bypass corresponde a los roles de administración
/// org/clínica (misma convención que ProgramActorContext).
/// </summary>
public record SosStaffActor(
    Guid UserId,
    Guid? ProfessionalId,
    Guid? ActiveClinicId,
    Guid? ActiveOrganizationId,
    bool BypassScope
);

/// <summary>
/// Atención por staff (aud=erp, permiso <c>Sos.Alerts.Manage</c> exigido por
/// el controller). El scope clínico sobre el paciente lo valida el handler
/// (D5): asignación directa, clínica u organización. Sin scope → 403 no
/// revelador; alerta ajena/inexistente → 404.
/// </summary>
public record AttendSosAlertCommand(Guid AlertId, SosStaffActor Staff)
    : IRequest<SosTransitionResult>;

public sealed class AttendSosAlertHandler(
    ISosAlertRepository repository,
    ILogger<AttendSosAlertHandler> logger
) : IRequestHandler<AttendSosAlertCommand, SosTransitionResult>
{
    public async Task<SosTransitionResult> Handle(
        AttendSosAlertCommand request,
        CancellationToken ct
    )
    {
        var alert = await repository.GetByIdAsync(request.AlertId, ct);
        if (alert is null)
        {
            return new SosTransitionResult(SosTransitionOutcome.NotFound, null);
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

        if (!scoped)
        {
            // (REQ-SOS-05) 403 no revelador: no se altera la alerta ni se
            // confirma información del paciente.
            logger.LogWarning(
                "SOS atendida rechazada por scope: alertId={AlertId}, staffUserId={StaffUserId}.",
                alert.Id,
                request.Staff.UserId
            );
            return new SosTransitionResult(SosTransitionOutcome.Forbidden, null);
        }

        if (alert.Status != SosAlertStatus.Activa)
        {
            return new SosTransitionResult(
                SosTransitionOutcome.NotActive,
                SosAlertDto.FromEntity(alert)
            );
        }

        // Compare-and-set atómico (misma semántica que la cancelación): una
        // carrera attend/cancel produce exactamente una terminal.
        var applied = await repository.AttendAsync(request.AlertId, request.Staff.UserId, ct);
        if (!applied)
        {
            var current = await repository.GetByIdAsync(request.AlertId, ct);
            return new SosTransitionResult(
                SosTransitionOutcome.NotActive,
                current is null ? null : SosAlertDto.FromEntity(current)
            );
        }

        var fresh = await repository.GetByIdAsync(request.AlertId, ct);

        logger.LogInformation(
            "SOS atendida por staff: alertId={AlertId}, patientId={PatientId}.",
            alert.Id,
            alert.PatientId
        );

        return new SosTransitionResult(
            SosTransitionOutcome.Transited,
            fresh is null ? null : SosAlertDto.FromEntity(fresh)
        );
    }
}
