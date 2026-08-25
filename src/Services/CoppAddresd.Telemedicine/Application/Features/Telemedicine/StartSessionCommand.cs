using CoppAddresd.Telemedicine.Application.Configuration;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Inicia la sesión de video de una cita: crea la sesión activa, asegura la sala
/// virtual (creándola si no existe) y pasa la cita a <c>InProgress</c>. Solo el
/// profesional de la cita o un supervisor pueden iniciarla; el paciente no.
/// La transición de estado se protege con el token de concurrencia de la cita
/// (dos inicios simultáneos → 409 para el perdedor).
/// </summary>
public sealed record StartSessionCommand(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<AppointmentDto>;

public sealed class StartSessionCommandValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class StartSessionCommandHandler(
    IAppointmentRepository appointments,
    IVideoProvider videoProvider,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IOptions<TelemedicineOptions> options)
    : IRequestHandler<StartSessionCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(StartSessionCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        SessionSupport.EnsureCanStartOrJoin(appointment.Status);

        var settings = await settingsProvider.GetSettingsAsync(appointment.OrganizationId, appointment.ClinicId, ct);
        var now = DateTimeOffset.UtcNow;
        SessionSupport.EnsureWithinWindow(appointment, settings, now);

        if (appointment.Sessions.Any(s => s.Status == TelemedicineSessionStatus.Active))
        {
            throw new BusinessRuleViolationException("Ya hay una sesión activa para esta cita.");
        }

        var room = appointment.Room;
        if (room is null)
        {
            var providerRoomName = SessionSupport.ProviderRoomName(appointment.Id);

            var providerRoom = await videoProvider.CreateRoomAsync(new RoomRequest(
                RoomName: providerRoomName,
                Type: VideoRoomType.Group,
                MaxParticipants: settings.MaxParticipants,
                EndTime: null,
                StatusCallbackUrl: string.IsNullOrWhiteSpace(options.Value.WebhookUrl)
                    ? null
                    : options.Value.WebhookUrl), ct);

            room = SessionSupport.NewRoom(
                appointment, settings, providerRoom.ProviderRoomName, providerRoom.ProviderRoomSid, request.UserId);

            appointment.Room = room;
        }

        var session = new TelemedicineSession
        {
            AppointmentId = appointment.Id,
            RoomId = room.Id,
            Status = TelemedicineSessionStatus.Active,
            StartedAt = now,
            CreatedBy = request.UserId,
        };
        appointment.Sessions.Add(session);

        appointment.Status = AppointmentStatus.InProgress;
        appointment.UpdatedAt = now.UtcDateTime;

        await appointments.UpdateAsync(appointment, ct);

        var dto = await AppointmentMapper.BuildDtosAsync([appointment], referenceData, ct);
        return dto[0];
    }
}
