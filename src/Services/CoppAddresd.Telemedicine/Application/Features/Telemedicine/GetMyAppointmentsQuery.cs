using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Citas del paciente autenticado ("Mis citas"), paginadas y con filtros por
/// estado y rango. El paciente se resuelve por la identidad del JWT
/// (<c>ActingUserId</c>), nunca por un id del cliente; sin perfil de paciente
/// resoluble → 403.
/// </summary>
public sealed record GetMyAppointmentsQuery(
    Guid ActingUserId,
    AppointmentStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize
) : IRequest<PaginatedAdminAppointmentsResult>;

public sealed class GetMyAppointmentsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IRoomRepository rooms
) : IRequestHandler<GetMyAppointmentsQuery, PaginatedAdminAppointmentsResult>
{
    public async Task<PaginatedAdminAppointmentsResult> Handle(
        GetMyAppointmentsQuery request,
        CancellationToken ct
    )
    {
        var patient = await referenceData.GetPatientByUserIdAsync(request.ActingUserId, ct);
        if (patient is null)
        {
            throw new ForbiddenException(
                "Solo los pacientes pueden consultar sus citas desde la app móvil."
            );
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await appointments.ListAdminAsync(
            professionalId: null,
            patientId: patient.Id,
            clinicId: null,
            locationId: null,
            status: request.Status,
            from: request.From?.ToUniversalTime(),
            to: request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct
        );

        var dtos = await AppointmentMapper.BuildDtosAsync(items, referenceData, ct);

        // Pre-join de la app móvil: el listado del paciente expone la ventana
        // efectiva de la sala (el detalle ya la trae; las listas admin la
        // omiten). Si la sala ya existe, su ventana persistida es la autoridad;
        // si no, se calcula con los settings efectivos y la reapertura
        // (SessionSupport.Window).
        var persistedRooms = await rooms.ListByAppointmentIdsAsync(
            items.Select(a => a.Id).ToList(),
            ct
        );
        var roomByAppointmentId = persistedRooms.ToDictionary(r => r.AppointmentId);

        // Los settings se resuelven una vez por contexto (org/clínica) para toda
        // la página: la ventana se calcula solo si no hay sala persistida y la
        // gracia de reapertura (F5) se expone siempre (contrato homogéneo).
        var settingsByContext = new Dictionary<(Guid Org, Guid? Clinic), TelemedicineSettings>();
        foreach (var context in items
            .Select(a => (Org: a.OrganizationId, Clinic: a.ClinicId))
            .Distinct())
        {
            settingsByContext[context] = await settingsProvider.GetSettingsAsync(
                context.Org,
                context.Clinic,
                ct
            );
        }

        var enriched = items
            .Zip(
                dtos,
                (entity, dto) =>
                {
                    var settings = settingsByContext[(entity.OrganizationId, entity.ClinicId)];

                    if (roomByAppointmentId.TryGetValue(entity.Id, out var room))
                    {
                        return dto with
                        {
                            RoomOpensAt = room.ScheduledOpenAt,
                            RoomClosesAt = room.ScheduledCloseAt,
                            ReopenGraceMinutes = settings.ReopenGraceMinutes,
                        };
                    }

                    var (open, close) = SessionSupport.Window(entity, settings);
                    return dto with
                    {
                        RoomOpensAt = open,
                        RoomClosesAt = close,
                        ReopenGraceMinutes = settings.ReopenGraceMinutes,
                    };
                }
            )
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminAppointmentsResult(enriched, total, page, pageSize, totalPages);
    }
}
