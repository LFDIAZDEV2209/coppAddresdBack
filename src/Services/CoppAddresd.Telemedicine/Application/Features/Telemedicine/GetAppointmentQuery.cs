using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Detalle de una cita, enriquecido con los datos de referencia del ERP.</summary>
public sealed record GetAppointmentQuery(Guid AppointmentId)
    : IRequest<AppointmentDto>;

public sealed class GetAppointmentQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider)
    : IRequestHandler<GetAppointmentQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(
        GetAppointmentQuery request,
        CancellationToken ct)
    {
        var entity = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        var dto = await AppointmentMapper.BuildDtosAsync([entity], referenceData, ct);

        // Ventana efectiva de la sala (settings por organización/clínica): el
        // detalle la expone para que la UI decida unirse sin esperar la creación
        // lazy de la sala. Las listas la dejan en null.
        var settings = await settingsProvider.GetSettingsAsync(
            entity.OrganizationId, entity.ClinicId, ct);

        return dto[0] with
        {
            RoomOpensAt = entity.ScheduledStart.AddMinutes(-settings.RoomOpenBeforeMinutes),
            RoomClosesAt = entity.ScheduledEnd.AddMinutes(settings.RoomCloseAfterMinutes),
            CompletedAt = entity.CompletedAt,
        };
    }
}
