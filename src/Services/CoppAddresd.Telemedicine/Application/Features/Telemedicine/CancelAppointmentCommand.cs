using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Cancela una cita. Registra el historial inmutable en
/// <c>appointment_cancellations</c> y actualiza el estado vigente de la cita.
/// No se puede cancelar una cita completada, cancelada o no-show.
/// </summary>
/// <remarks>
/// Alcance dual: con <c>PatientUserId</c> nulo (ERP con permiso
/// <c>Appointments.AppointmentsCancel</c>) conserva el comportamiento actual;
/// con <c>PatientUserId</c> presente (paciente de la app móvil) exige que la
/// cita pertenezca al paciente del JWT (403), que el estado sea
/// <c>Confirmed</c>/<c>Requested</c> (409) y fuerza <c>CancelledBy.Patient</c>.
/// </remarks>
public sealed record CancelAppointmentCommand(
    Guid AppointmentId,
    string Reason,
    CancelledBy CancelledBy,
    Guid? CancelledByUserId,
    Guid? PatientUserId = null
) : IRequest<AppointmentDto>;

public sealed class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la cancelación es obligatorio.")
            .MaximumLength(2000);
    }
}

public sealed class CancelAppointmentCommandHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    IAlertRepository alerts
) : IRequestHandler<CancelAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(CancelAppointmentCommand request, CancellationToken ct)
    {
        var entity =
            await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        // Alcance paciente (app móvil): la cita debe ser del paciente del JWT y
        // solo admite estados Confirmado/Solicitado.
        var cancelledBy = request.CancelledBy;
        if (request.PatientUserId is { } patientUserId)
        {
            var actingPatient = await referenceData.GetPatientByUserIdAsync(patientUserId, ct);
            if (actingPatient is null || actingPatient.Id != entity.PatientId)
            {
                throw new ForbiddenException(
                    "Solo el paciente de la cita puede cancelarla desde la app móvil."
                );
            }

            if (entity.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.Requested))
            {
                throw new BusinessRuleViolationException(
                    $"El paciente no puede cancelar la cita en su estado actual ({entity.Status})."
                );
            }

            cancelledBy = CancelledBy.Patient;
        }

        if (
            entity.Status
            is AppointmentStatus.Completed
                or AppointmentStatus.Cancelled
                or AppointmentStatus.NoShow
        )
        {
            throw new BusinessRuleViolationException(
                $"La cita no puede cancelarse en su estado actual ({entity.Status})."
            );
        }

        var now = DateTimeOffset.UtcNow;

        entity.Status = AppointmentStatus.Cancelled;
        entity.CancellationReason = request.Reason;
        entity.CancelledBy = cancelledBy;
        entity.CancelledAt = now;
        entity.UpdatedAt = now.UtcDateTime;

        // Integridad del registro clínico: un borrador de encuentro de una
        // consulta cancelada no debe quedar huérfano → se cancela. Un encuentro
        // COMPLETADO se preserva (la consulta ocurrió y su registro es final).
        if (entity.Encounter is { Status: EncounterStatus.Draft } encounter)
        {
            encounter.Status = EncounterStatus.Cancelled;
            encounter.UpdatedAt = now.UtcDateTime;
        }

        entity.Cancellations.Add(
            new AppointmentCancellation
            {
                AppointmentId = entity.Id,
                CancelledBy = cancelledBy,
                CancelledByUserId = request.CancelledByUserId,
                Reason = request.Reason,
                CancelledAt = now,
            }
        );

        await appointments.UpdateAsync(entity, ct);

        // Bandeja: cancelación → al profesional asignado.
        var professional = await referenceData.GetProfessionalAsync(entity.ProfessionalId, ct);
        var patient = await referenceData.GetPatientAsync(entity.PatientId, ct);
        if (
            AlertMaterializer.AppointmentCancelled(
                professional?.UserId,
                entity.Id,
                patient?.FullName ?? "el paciente",
                request.Reason
            ) is
            { } alert
        )
        {
            await alerts.AddRangeAsync([alert], ct);
        }

        var specialty = await referenceData.GetSpecialtyAsync(entity.SpecialtyId, ct);
        var location = entity.LocationId is { } locationId
            ? await referenceData.GetLocationAsync(locationId, ct)
            : null;

        return new AppointmentDto(
            entity.Id,
            entity.RequestId,
            entity.PatientId,
            patient?.FullName,
            entity.ProfessionalId,
            professional?.FullName,
            entity.SpecialtyId,
            specialty?.Name,
            entity.OrganizationId,
            entity.ClinicId,
            entity.LocationId,
            location?.Name,
            entity.ScheduledStart,
            entity.ScheduledEnd,
            entity.DurationMinutes,
            entity.Status,
            entity.RescheduleCount,
            entity.CancellationReason,
            entity.CreatedAt
        );
    }
}
