using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// El profesional agenda directamente una cita para un paciente (sin solicitud
/// previa). La cita nace <c>Confirmed</c>. Reglas: referencias válidas,
/// duración/anticipación parametrizadas y sin solapamiento con otras citas
/// activas del profesional.
/// </summary>
public sealed record ScheduleAppointmentCommand(
    Guid PatientId,
    Guid ProfessionalId,
    Guid SpecialtyId,
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes,
    Guid CreatedBy) : IRequest<AppointmentDto>;

public sealed class ScheduleAppointmentCommandValidator
    : AbstractValidator<ScheduleAppointmentCommand>
{
    public ScheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ProfessionalId).NotEmpty();
        RuleFor(x => x.SpecialtyId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ScheduledStart).NotEmpty();
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}

public sealed class ScheduleAppointmentCommandHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IAlertRepository alerts,
    ITelemedicineMetricsQueue? metricsQueue = null)
    : IRequestHandler<ScheduleAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(
        ScheduleAppointmentCommand request,
        CancellationToken ct)
    {
        var patient = await ReferenceDataGuard.RequirePatientAsync(referenceData, request.PatientId, ct);
        var professional = await ReferenceDataGuard.RequireProfessionalAsync(referenceData, request.ProfessionalId, ct);
        var specialty = await ReferenceDataGuard.RequireSpecialtyAsync(referenceData, request.SpecialtyId, ct);
        await ReferenceDataGuard.RequireLocationAsync(referenceData, request.LocationId, ct);

        var settings = await settingsProvider.GetSettingsAsync(request.OrganizationId, request.ClinicId, ct);
        var (start, end, duration) = SchedulingRules.ResolveSlot(
            request.ScheduledStart, request.DurationMinutes, settings, DateTimeOffset.UtcNow);

        if (await appointments.HasActiveOverlapAsync(request.ProfessionalId, start, end, null, ct))
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita en ese horario.");
        }

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            ProfessionalId = request.ProfessionalId,
            SpecialtyId = request.SpecialtyId,
            OrganizationId = request.OrganizationId,
            ClinicId = request.ClinicId,
            LocationId = request.LocationId,
            ScheduledStart = start,
            ScheduledEnd = end,
            DurationMinutes = duration,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = request.CreatedBy,
        };

        await appointments.AddAsync(appointment, ct);

        // Métricas pre-agregadas CQRS en segundo plano (0ms impacto en escritura)
        if (metricsQueue != null)
        {
            await metricsQueue.EnqueueAsync(new AppointmentScheduledMetricEvent(
                appointment.Id,
                appointment.ProfessionalId,
                appointment.ClinicId,
                DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime),
                appointment.ScheduledStart.UtcDateTime.Hour,
                appointment.Status
            ));
        }

        // Bandeja: cita creada → al profesional asignado.
        if (AlertMaterializer.NewAppointment(
                professional.UserId,
                appointment.Id,
                appointment.SpecialtyId,
                patient.FullName,
                specialty.Name,
                appointment.ScheduledStart) is { } alert)
        {
            await alerts.AddRangeAsync([alert], ct);
        }

        return new AppointmentDto(
            appointment.Id,
            null,
            appointment.PatientId,
            patient.FullName,
            appointment.ProfessionalId,
            professional.FullName,
            appointment.SpecialtyId,
            specialty.Name,
            appointment.OrganizationId,
            appointment.ClinicId,
            appointment.LocationId,
            null,
            appointment.ScheduledStart,
            appointment.ScheduledEnd,
            appointment.DurationMinutes,
            appointment.Status,
            appointment.RescheduleCount,
            appointment.CancellationReason,
            appointment.CreatedAt);
    }
}
