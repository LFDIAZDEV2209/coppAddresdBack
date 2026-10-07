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
        // Dos referencias por sala, independientemente del número de participantes.
        // La identidad del proveedor es el usuario Auth, no el perfil clínico.
        var patient = await referenceData.GetPatientAsync(appointment.PatientId, ct);
        var professional = await referenceData.GetProfessionalAsync(appointment.ProfessionalId, ct);

        var participantDtos = participants
            .Select(p => new RoomParticipantDto(
                p.ParticipantSid,
                p.Identity,
                p.IsConnected,
                p.ConnectedAt,
                p.DisconnectedAt,
                Guid.TryParse(p.Identity, out var userId) && userId == patient?.UserId
                    ? patient.FullName
                    : Guid.TryParse(p.Identity, out userId) && userId == professional?.UserId
                        ? professional.FullName : null,
                Guid.TryParse(p.Identity, out userId) && userId == patient?.UserId
                    ? "Patient"
                    : Guid.TryParse(p.Identity, out userId) && userId == professional?.UserId
                        ? "Professional" : "Supervisor"))
            .ToList();

        return RoomDtos.Build(room, participantDtos);
    }
}
