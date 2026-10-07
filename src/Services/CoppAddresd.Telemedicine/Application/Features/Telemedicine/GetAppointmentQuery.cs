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
    ITelemedicineSettingsProvider settingsProvider,
    IRoomRepository rooms)
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
        // detalle la expone con la sesión activa, o la calcula si aún no hay sala.
        // Las listas admin la dejan en null; la del paciente
        // (app móvil) también la trae para el pre-join (GetMyAppointmentsQuery).
        var settings = await settingsProvider.GetSettingsAsync(
            entity.OrganizationId, entity.ClinicId, ct);

        var room = await rooms.GetByAppointmentIdAsync(entity.Id, includeSessions: true, ct: ct);
        var (open, close) = SessionSupport.EffectiveWindow(entity, settings, room);

        return dto[0] with
        {
            RoomOpensAt = open,
            RoomClosesAt = close,
            CompletedAt = entity.CompletedAt,
            // F5: el detalle expone la gracia efectiva (útil en Completed) para
            // que el ERP calcule "Reabrir consulta" sin hardcodear 60.
            ReopenGraceMinutes = settings.ReopenGraceMinutes,
        };
    }
}
