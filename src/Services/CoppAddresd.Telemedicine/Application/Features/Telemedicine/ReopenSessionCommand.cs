using CoppAddresd.Telemedicine.Application.Configuration;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Reabre una consulta completada dentro de la gracia de
/// <see cref="ReopenSessionCommandHandler.ReopenGraceMinutes"/>: el profesional
/// asignado o un supervisor con permiso pueden volver a la sala. La sala del
/// proveedor anterior quedó completada (irreversible), así que se crea una sala
/// nueva y la ventana de acceso corre desde la reapertura.
/// </summary>
public sealed record ReopenSessionCommand(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<AppointmentDto>;

public sealed class ReopenSessionCommandValidator : AbstractValidator<ReopenSessionCommand>
{
    public ReopenSessionCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class ReopenSessionCommandHandler(
    IAppointmentRepository appointments,
    IRoomRepository rooms,
    IVideoProvider videoProvider,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IOptions<TelemedicineOptions> options,
    ILogger<ReopenSessionCommandHandler> logger,
    ITelemedicineMetricsQueue? metricsQueue = null)
    : IRequestHandler<ReopenSessionCommand, AppointmentDto>
{
    /// <summary>Minutos de gracia tras completar la cita en que se puede reabrir.</summary>
    public const int ReopenGraceMinutes = 60;

    public async Task<AppointmentDto> Handle(ReopenSessionCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new BusinessRuleViolationException(
                $"Solo las citas completadas pueden reabrirse (estado actual: {appointment.Status}).");
        }

        var now = DateTimeOffset.UtcNow;
        var completedAt = appointment.CompletedAt
            ?? (appointment.UpdatedAt is { } updatedAt
                ? new DateTimeOffset(updatedAt, TimeSpan.Zero)
                : appointment.ScheduledEnd);

        if (now - completedAt > TimeSpan.FromMinutes(ReopenGraceMinutes))
        {
            throw new BusinessRuleViolationException(
                $"La ventana para reabrir la consulta expiró ({ReopenGraceMinutes} minutos desde la finalización).");
        }

        var settings = await settingsProvider.GetSettingsAsync(
            appointment.OrganizationId, appointment.ClinicId, ct);
        SessionSupport.EnsureValidMaxParticipants(settings.MaxParticipants);

        var oldStatus = appointment.Status;
        appointment.ReopenCount++;
        appointment.ReopenedAt = now;
        appointment.Status = AppointmentStatus.InProgress;
        appointment.CompletedAt = null; // el ciclo nuevo vuelve a empezar
        appointment.UpdatedAt = now.UtcDateTime;

        // Sala nueva: la del proveedor anterior quedó completada (irreversible).
        var roomName = SessionSupport.ProviderRoomName(appointment.Id, appointment.ReopenCount);
        var providerRoom = await videoProvider.CreateRoomAsync(
            new RoomRequest(
                RoomName: roomName,
                Type: VideoRoomType.Group,
                MaxParticipants: settings.MaxParticipants,
                EndTime: null,
                StatusCallbackUrl: string.IsNullOrWhiteSpace(options.Value.WebhookUrl)
                    ? null
                    : options.Value.WebhookUrl),
            ct);

        var room = await rooms.GetForUpdateAsync(appointment.Id, ct);
        if (room is null)
        {
            room = SessionSupport.NewRoom(
                appointment, settings, roomName, providerRoom.ProviderRoomSid, request.UserId);
            await rooms.AddAsync(room, ct);
        }
        else
        {
            var (open, close) = SessionSupport.Window(appointment, settings);
            room.ProviderRoomSid = providerRoom.ProviderRoomSid;
            room.ProviderRoomName = roomName;
            room.Status = VirtualRoomStatus.Created;
            room.PatientJoinedAt = null;
            room.ScheduledOpenAt = open;
            room.ScheduledCloseAt = close;
            // F3: la sala nueva del proveedor se creó con el settings vigente; la
            // fila persistida debe reflejarlo (antes quedaba con el valor viejo).
            room.MaxParticipants = settings.MaxParticipants;
            room.UpdatedAt = now.UtcDateTime;
            await rooms.UpdateAsync(room, ct);
        }

        await appointments.UpdateAsync(appointment, ct);

        if (metricsQueue != null)
        {
            await metricsQueue.EnqueueAsync(new AppointmentStatusChangedMetricEvent(
                appointment.Id,
                appointment.ProfessionalId,
                appointment.ClinicId,
                DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime),
                oldStatus,
                AppointmentStatus.InProgress
            ));
        }

        logger.LogInformation(
            "Consulta {AppointmentId} reabierta (reapertura #{ReopenCount}) por {UserId}.",
            appointment.Id, appointment.ReopenCount, request.UserId);

        var dto = await AppointmentMapper.BuildDtosAsync([appointment], referenceData, ct);
        return dto[0];
    }
}
