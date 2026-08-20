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
public sealed record RescheduleTelemedicineAppointmentCommand(
    Guid AppointmentId,
    DateTimeOffset NewStart,
    int? DurationMinutes,
    string? Reason,
    RescheduleRequestedBy RequestedBy,
    Guid? RequestedByUserId) : IRequest<TelemedicineAppointmentDto>;

public sealed class RescheduleTelemedicineAppointmentCommandValidator
    : AbstractValidator<RescheduleTelemedicineAppointmentCommand>
{
    public RescheduleTelemedicineAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewStart).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public sealed class RescheduleTelemedicineAppointmentCommandHandler(
    IAppointmentRepository appointments,
    ITelemedicineReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider)
    : IRequestHandler<RescheduleTelemedicineAppointmentCommand, TelemedicineAppointmentDto>
{
    public async Task<TelemedicineAppointmentDto> Handle(
        RescheduleTelemedicineAppointmentCommand request,
        CancellationToken ct)
    {
        var entity = await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        if (entity.Status != AppointmentStatus.Confirmed)
        {
            throw new BusinessRuleViolationException(
                $"Solo las citas confirmadas pueden reprogramarse (estado actual: {entity.Status}).");
        }

        var settings = await settingsProvider.GetSettingsAsync(entity.OrganizationId, entity.ClinicId, ct);

        if (entity.RescheduleCount >= settings.MaxReschedules)
        {
            throw new BusinessRuleViolationException(
                $"Se alcanzó el límite de {settings.MaxReschedules} reprogramaciones para esta cita.");
        }

        var (start, end, duration) = SchedulingRules.ResolveSlot(
            request.NewStart,
            request.DurationMinutes ?? entity.DurationMinutes,
            settings,
            DateTimeOffset.UtcNow);

        if (await appointments.HasActiveOverlapAsync(
                entity.ProfessionalId, start, end, excludeAppointmentId: entity.Id, ct))
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita que se solapa con el nuevo horario.");
        }

        var fromStart = entity.ScheduledStart;
        var now = DateTimeOffset.UtcNow;

        entity.ScheduledStart = start;
        entity.ScheduledEnd = end;
        entity.DurationMinutes = duration;
        entity.RescheduleCount++;
        entity.UpdatedAt = now.UtcDateTime;

        entity.Reschedules.Add(new AppointmentReschedule
        {
            AppointmentId = entity.Id,
            RequestedBy = request.RequestedBy,
            RequestedByUserId = request.RequestedByUserId,
            FromStart = fromStart,
            ToStart = start,
            Reason = request.Reason,
            RescheduledAt = now,
        });

        await appointments.UpdateAsync(entity, ct);

        var patient = await referenceData.GetPatientAsync(entity.PatientId, ct);
        var professional = await referenceData.GetProfessionalAsync(entity.ProfessionalId, ct);
        var specialty = await referenceData.GetSpecialtyAsync(entity.SpecialtyId, ct);
        var location = entity.LocationId is { } locationId
            ? await referenceData.GetLocationAsync(locationId, ct)
            : null;

        return new TelemedicineAppointmentDto(
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
            entity.CreatedAt);
    }
}
