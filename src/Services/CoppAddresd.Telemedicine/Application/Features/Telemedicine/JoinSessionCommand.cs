using CoppAddresd.Telemedicine.Application.Configuration;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Genera el token de acceso a la sala virtual de una cita para el usuario
/// autenticado. Verifica: cita existente, participante autorizado (profesional,
/// paciente o supervisor), estado de la cita y ventana de acceso. La sala se
/// crea perezosamente en el primer <c>join-token</c> dentro de la ventana
/// (creación idempotente por nombre determinista). El usuario autenticado sale
/// del JWT, nunca del cuerpo de la petición.
/// </summary>
public sealed record JoinSessionCommand(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<JoinSessionResultDto>;

public sealed class JoinSessionCommandValidator : AbstractValidator<JoinSessionCommand>
{
    public JoinSessionCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class JoinSessionCommandHandler(
    IAppointmentRepository appointments,
    IRoomRepository rooms,
    IVideoProvider videoProvider,
    ITelemedicineReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IOptions<TelemedicineOptions> options)
    : IRequestHandler<JoinSessionCommand, JoinSessionResultDto>
{
    public async Task<JoinSessionResultDto> Handle(JoinSessionCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        SessionSupport.EnsureCanStartOrJoin(appointment.Status);

        var settings = await settingsProvider.GetSettingsAsync(appointment.OrganizationId, appointment.ClinicId, ct);
        var now = DateTimeOffset.UtcNow;
        SessionSupport.EnsureWithinWindow(appointment, settings, now);

        var room = await rooms.GetByAppointmentIdAsync(appointment.Id, includeSessions: true, ct);
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

            room = await rooms.AddAsync(room, ct);
        }

        var token = await videoProvider.GenerateAccessTokenAsync(new AccessTokenRequest(
            Identity: request.UserId.ToString(),
            RoomName: room.ProviderRoomName,
            TtlSeconds: settings.AccessTokenTtlSeconds), ct);

        return new JoinSessionResultDto(
            token,
            now.AddSeconds(settings.AccessTokenTtlSeconds),
            RoomDtos.Build(room));
    }
}
