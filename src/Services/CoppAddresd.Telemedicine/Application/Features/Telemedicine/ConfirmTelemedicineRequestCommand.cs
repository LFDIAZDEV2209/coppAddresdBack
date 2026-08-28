using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// El profesional confirma una solicitud pendiente: valida disponibilidad y
/// crea la cita <c>Confirmed</c> vinculada a la solicitud (que pasa a
/// <c>Converted</c>). La integridad "una solicitud → una cita" se garantiza con
/// un índice único sobre <c>appointment.request_id</c> (anti doble confirmación
/// ante reintentos).
/// </summary>
public sealed record ConfirmTelemedicineRequestCommand(
    Guid RequestId,
    Guid ProfessionalId,
    DateTimeOffset ScheduledStart,
    int? DurationMinutes,
    Guid? LocationId,
    Guid CreatedBy) : IRequest<AppointmentDto>;

public sealed class ConfirmTelemedicineRequestCommandValidator
    : AbstractValidator<ConfirmTelemedicineRequestCommand>
{
    public ConfirmTelemedicineRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ProfessionalId).NotEmpty();
        RuleFor(x => x.CreatedBy).NotEmpty();
        RuleFor(x => x.ScheduledStart).NotEmpty();
    }
}

public sealed class ConfirmTelemedicineRequestCommandHandler(
    IRequestRepository requests,
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IAlertRepository alerts)
    : IRequestHandler<ConfirmTelemedicineRequestCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(
        ConfirmTelemedicineRequestCommand request,
        CancellationToken ct)
    {
        var entity = await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new NotFoundException("Solicitud", request.RequestId);

        if (entity.Status is not (AppointmentRequestStatus.Pending or AppointmentRequestStatus.Approved))
        {
            throw new BusinessRuleViolationException(
                $"La solicitud no puede confirmarse en su estado actual ({entity.Status}).");
        }

        var patient = await ReferenceDataGuard.RequirePatientAsync(referenceData, entity.PatientId, ct);
        var professional = await ReferenceDataGuard.RequireProfessionalAsync(referenceData, request.ProfessionalId, ct);
        var specialty = await ReferenceDataGuard.RequireSpecialtyAsync(referenceData, entity.SpecialtyId, ct);
        await ReferenceDataGuard.RequireLocationAsync(referenceData, request.LocationId, ct);

        var settings = await settingsProvider.GetSettingsAsync(entity.OrganizationId, entity.ClinicId, ct);
        var (start, end, duration) = SchedulingRules.ResolveSlot(
            request.ScheduledStart, request.DurationMinutes, settings, DateTimeOffset.UtcNow);

        // Verificación en aplicación (mensaje amigable); el constraint de
        // exclusión en BD es la garantía real ante dos confirmaciones simultáneas.
        if (await appointments.HasActiveOverlapAsync(request.ProfessionalId, start, end, null, ct))
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita en ese horario.");
        }

        var appointment = new Appointment
        {
            RequestId = entity.Id,
            PatientId = entity.PatientId,
            ProfessionalId = request.ProfessionalId,
            SpecialtyId = entity.SpecialtyId,
            OrganizationId = entity.OrganizationId,
            ClinicId = entity.ClinicId,
            LocationId = request.LocationId,
            ScheduledStart = start,
            ScheduledEnd = end,
            DurationMinutes = duration,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = request.CreatedBy,
        };

        await appointments.AddAsync(appointment, ct);

        // Marca la solicitud como convertida (UPDATE dirigido; el índice único
        // sobre request_id evita una doble confirmación si este paso fallara).
        await requests.SetStatusAsync(entity.Id, AppointmentRequestStatus.Converted, ct);

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
            appointment.RequestId,
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
