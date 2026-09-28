using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Reprogramación inmediata de una cita confirmada: valida el nuevo horario
/// (sin solapamiento, ventanas de anticipación), registra el cambio en
/// <c>appointment_reschedules</c> (historial append-only) e incrementa
/// <c>RescheduleCount</c> (límite parametrizado en la configuración).
/// </summary>
/// <remarks>
/// Alcance dual: con <c>PatientUserId</c> nulo (ERP con permiso
/// <c>Appointments.AppointmentsReschedule</c>) conserva el comportamiento actual;
/// con <c>PatientUserId</c> presente (paciente de la app móvil) exige que la
/// cita pertenezca al paciente del JWT (403, resuelto vía
/// <c>GetPatientByUserIdAsync</c>: nunca se compara el id de cita con el id de
/// usuario Auth) y fuerza <c>RequestedBy.Patient</c> desde el servidor.
/// </remarks>
public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTimeOffset NewStart,
    int? DurationMinutes,
    string? Reason,
    RescheduleRequestedBy RequestedBy,
    Guid? RequestedByUserId,
    Guid? PatientUserId = null
) : IRequest<AppointmentDto>;

public sealed class RescheduleAppointmentCommandValidator
    : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewStart).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public sealed class RescheduleAppointmentCommandHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IAlertRepository alerts
) : IRequestHandler<RescheduleAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(
        RescheduleAppointmentCommand request,
        CancellationToken ct
    )
    {
        var entity =
            await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        if (entity.Status != AppointmentStatus.Confirmed)
        {
            throw new BusinessRuleViolationException(
                $"Solo las citas confirmadas pueden reprogramarse (estado actual: {entity.Status})."
            );
        }

        // Alcance paciente (app móvil): la cita debe ser del paciente del JWT.
        // El perfil se resuelve por usuario Auth; el id de la cita (patient_profiles)
        // nunca se compara directo con el id de usuario.
        var requestedBy = request.RequestedBy;
        if (request.PatientUserId is { } patientUserId)
        {
            var actingPatient = await referenceData.GetPatientByUserIdAsync(patientUserId, ct);
            if (actingPatient is null || actingPatient.Id != entity.PatientId)
            {
                throw new ForbiddenException(
                    "Solo el paciente de la cita puede reprogramarla desde la app móvil."
                );
            }

            requestedBy = RescheduleRequestedBy.Patient;
        }

        var settings = await settingsProvider.GetSettingsAsync(
            entity.OrganizationId,
            entity.ClinicId,
            ct
        );

        if (entity.RescheduleCount >= settings.MaxReschedules)
        {
            throw new BusinessRuleViolationException(
                $"Se alcanzó el límite de {settings.MaxReschedules} reprogramaciones para esta cita."
            );
        }

        var fromStart = entity.ScheduledStart;
        var now = DateTimeOffset.UtcNow;

        // La cita original también debe estar fuera de la ventana de
        // anticipación mínima: dentro de ella ya no se reprograma.
        if (fromStart < now.AddHours(settings.MinAdvanceBookingHours))
        {
            throw new BusinessRuleViolationException(
                $"La cita está dentro de la ventana de anticipación mínima ({settings.MinAdvanceBookingHours} h) y ya no puede reprogramarse."
            );
        }

        var (start, end, duration) = SchedulingRules.ResolveSlot(
            request.NewStart,
            request.DurationMinutes ?? entity.DurationMinutes,
            settings,
            now
        );

        if (
            await appointments.HasActiveOverlapAsync(
                entity.ProfessionalId,
                start,
                end,
                excludeAppointmentId: entity.Id,
                ct
            )
        )
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita que se solapa con el nuevo horario."
            );
        }

        entity.ScheduledStart = start;
        entity.ScheduledEnd = end;
        entity.DurationMinutes = duration;
        entity.RescheduleCount++;
        entity.UpdatedAt = now.UtcDateTime;

        entity.Reschedules.Add(
            new AppointmentReschedule
            {
                AppointmentId = entity.Id,
                RequestedBy = requestedBy,
                RequestedByUserId = request.RequestedByUserId,
                FromStart = fromStart,
                ToStart = start,
                Reason = request.Reason,
                RescheduledAt = now,
            }
        );

        await appointments.UpdateAsync(entity, ct);

        // Bandeja: reprogramación → al profesional asignado.
        var professional = await referenceData.GetProfessionalAsync(entity.ProfessionalId, ct);
        var patient = await referenceData.GetPatientAsync(entity.PatientId, ct);
        if (
            AlertMaterializer.AppointmentRescheduled(
                professional?.UserId,
                entity.Id,
                patient?.FullName ?? "el paciente",
                start
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
