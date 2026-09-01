using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Decisión de revisión de una solicitud de telemedicina.</summary>
public enum RequestDecision
{
    Approved,
    Rejected,
}

/// <summary>
/// Primer paso del ciclo de revisión de una solicitud (2 pasos: revisar y luego
/// confirmar con cita). <c>Approved</c> marca la solicitud como aprobada sin
/// crear cita; <c>Rejected</c> la cierra con un motivo obligatorio. La
/// autorización es dual y se resuelve en el handler: el claim de permiso
/// <c>Appointments.AdminView</c> del JWT o el profesional asignado a la
/// solicitud (identidad del JWT, nunca un id del cliente).
/// </summary>
public sealed record ReviewTelemedicineRequestCommand(
    Guid RequestId,
    RequestDecision Decision,
    string? Reason,
    Guid ActorId,
    bool HasAdminView
) : IRequest<TelemedicineRequestDto>;

public sealed class ReviewTelemedicineRequestCommandValidator
    : AbstractValidator<ReviewTelemedicineRequestCommand>
{
    public ReviewTelemedicineRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();

        // El motivo es obligatorio solo al rechazar; la aprobación no lleva motivo.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del rechazo es obligatorio.")
            .MaximumLength(500)
            .When(x => x.Decision == RequestDecision.Rejected);
    }
}

public sealed class ReviewTelemedicineRequestCommandHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData,
    IAlertRepository alerts
) : IRequestHandler<ReviewTelemedicineRequestCommand, TelemedicineRequestDto>
{
    public async Task<TelemedicineRequestDto> Handle(
        ReviewTelemedicineRequestCommand request,
        CancellationToken ct
    )
    {
        var entity =
            await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new NotFoundException("Solicitud", request.RequestId);

        // Alcance dual: administrador con AdminView, o el profesional asignado
        // (resuelto por identidad del JWT — nunca por un id del cliente).
        if (!request.HasAdminView)
        {
            var professional = await referenceData.GetProfessionalByUserIdAsync(
                request.ActorId,
                ct
            );
            if (professional is null || professional.Id != entity.ProfessionalId)
            {
                throw new ForbiddenException("No tienes acceso a esta solicitud.");
            }
        }

        var patientName = (await referenceData.GetPatientAsync(entity.PatientId, ct))?.FullName;
        var specialtyName = (await referenceData.GetSpecialtyAsync(entity.SpecialtyId, ct))?.Name;

        if (request.Decision == RequestDecision.Approved)
        {
            if (entity.Status != AppointmentRequestStatus.Pending)
            {
                throw new BusinessRuleViolationException(
                    $"La solicitud no puede aprobarse en su estado actual ({entity.Status})."
                );
            }

            await requests.SetStatusAsync(entity.Id, AppointmentRequestStatus.Approved, ct);

            // La aprobación por un administrador avisa al profesional asignado
            // (RequestApproved); el profesional que aprueba su propia solicitud
            // no necesita auto-notificarse.
            if (request.HasAdminView && entity.ProfessionalId is { } professionalId)
            {
                var professional = await referenceData.GetProfessionalAsync(professionalId, ct);
                if (
                    AlertMaterializer.RequestApproved(
                        professional?.UserId,
                        entity.Id,
                        entity.SpecialtyId,
                        patientName ?? "El paciente",
                        specialtyName ?? "telemedicina"
                    ) is
                    { } alert
                )
                {
                    await alerts.AddRangeAsync([alert], ct);
                }
            }

            return BuildDto(
                entity,
                patientName,
                specialtyName,
                AppointmentRequestStatus.Approved,
                rejectionReason: null
            );
        }

        if (
            entity.Status
            is not (AppointmentRequestStatus.Pending or AppointmentRequestStatus.Approved)
        )
        {
            throw new BusinessRuleViolationException(
                $"La solicitud no puede rechazarse en su estado actual ({entity.Status})."
            );
        }

        await requests.SetRejectedAsync(entity.Id, request.Reason!, ct);

        // El rechazo avisa al profesional asignado (si es resoluble como usuario).
        if (entity.ProfessionalId is { } assignedProfessionalId)
        {
            var professional = await referenceData.GetProfessionalAsync(assignedProfessionalId, ct);
            if (
                AlertMaterializer.RequestRejected(
                    professional?.UserId,
                    entity.Id,
                    entity.SpecialtyId,
                    request.Reason!
                ) is
                { } alert
            )
            {
                await alerts.AddRangeAsync([alert], ct);
            }
        }

        return BuildDto(
            entity,
            patientName,
            specialtyName,
            AppointmentRequestStatus.Rejected,
            request.Reason
        );
    }

    /// <summary>
    /// Proyección del DTO con el estado destino de la transición (la entidad en
    /// memoria conserva el estado previo; el repositorio persiste con updates
    /// dirigidos sin rehidratar).
    /// </summary>
    private static TelemedicineRequestDto BuildDto(
        TelemedicineRequest entity,
        string? patientName,
        string? specialtyName,
        AppointmentRequestStatus status,
        string? rejectionReason
    ) =>
        new(
            entity.Id,
            entity.PatientId,
            patientName,
            entity.ProfessionalId,
            entity.SpecialtyId,
            specialtyName,
            entity.OrganizationId,
            entity.ClinicId,
            entity.LocationId,
            entity.PreferredStart,
            entity.Reason,
            status,
            entity.CreatedAt,
            rejectionReason
        );
}
