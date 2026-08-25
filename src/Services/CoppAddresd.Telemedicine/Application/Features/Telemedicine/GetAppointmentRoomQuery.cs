using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Detalle de la sala virtual de una cita (estado + ventana + participantes en vivo).</summary>
public sealed record GetAppointmentRoomQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission) : IRequest<VirtualRoomDto>;

public sealed class GetAppointmentRoomQueryHandler(
    IAppointmentRepository appointments,
    IRoomRepository rooms,
    IVideoProvider videoProvider,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetAppointmentRoomQuery, VirtualRoomDto>
{
    public async Task<VirtualRoomDto> Handle(GetAppointmentRoomQuery request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var room = await rooms.GetByAppointmentIdAsync(request.AppointmentId, includeSessions: true, ct)
            ?? throw new NotFoundException("Sala", request.AppointmentId);

        var participants = await videoProvider.GetParticipantsAsync(room.ProviderRoomSid, ct);

        var participantDtos = participants
            .Select(p => new RoomParticipantDto(
                p.ParticipantSid,
                p.Identity,
                p.IsConnected,
                p.ConnectedAt,
                p.DisconnectedAt))
            .ToList();

        return RoomDtos.Build(room, participantDtos);
    }
}
