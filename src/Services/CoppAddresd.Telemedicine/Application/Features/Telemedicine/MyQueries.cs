using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Listado de citas del profesional autenticado, paginado y con filtros
/// (paciente, sede, estado, rango). Es la variante "mis citas" del listado
/// administrativo: el <c>ProfessionalId</c> sale de la identidad del JWT en el
/// controlador, nunca de un id enviado por el cliente. Reutiliza el shape del
/// listado admin para que la UI sea idéntica cambiando solo el origen de datos.
/// </summary>
public sealed record ListMyAppointmentsQuery(
    Guid ProfessionalId,
    Guid? PatientId,
    Guid? LocationId,
    AppointmentStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedAdminAppointmentsResult>;

public sealed class ListMyAppointmentsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData
) : IRequestHandler<ListMyAppointmentsQuery, PaginatedAdminAppointmentsResult>
{
    public async Task<PaginatedAdminAppointmentsResult> Handle(
        ListMyAppointmentsQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Alcance forzado por identidad: el profesionalId del query es el del
        // JWT, no un filtro opcional del cliente.
        var (items, total) = await appointments.ListAdminAsync(
            request.ProfessionalId,
            request.PatientId,
            clinicId: null,
            request.LocationId,
            request.Status,
            request.From?.ToUniversalTime(),
            request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct
        );

        var dtos = await AppointmentMapper.BuildDtosAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminAppointmentsResult(dtos, total, page, pageSize, totalPages);
    }
}

/// <summary>
/// KPIs del dashboard "Mis citas" del profesional: mismo shape que
/// <see cref="AdminSummaryDto"/> pero con los conteos acotados al profesional
/// (citas, solicitudes y sesiones activas propias; alertas sin leer de su
/// usuario). El alcance sale de la identidad del JWT en el controlador.
/// </summary>
public sealed record GetMySummaryQuery(Guid ProfessionalId, Guid UserId)
    : IRequest<AdminSummaryDto>;

public sealed class GetMySummaryQueryHandler(
    IAppointmentRepository appointments,
    IRequestRepository requests,
    IAlertRepository alerts,
    IRoomRepository rooms
) : IRequestHandler<GetMySummaryQuery, AdminSummaryDto>
{
    public async Task<AdminSummaryDto> Handle(GetMySummaryQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1);

        var appointmentsToday = await appointments.CountInRangeAsync(
            request.ProfessionalId,
            startOfDay,
            endOfDay,
            ct
        );
        var appointmentsPending = await appointments.CountByStatusAsync(
            AppointmentStatus.Confirmed,
            request.ProfessionalId,
            ct
        );
        var appointmentsCompleted = await appointments.CountByStatusAsync(
            AppointmentStatus.Completed,
            request.ProfessionalId,
            ct
        );
        var requestsPending = await requests.CountByStatusAsync(
            AppointmentRequestStatus.Pending,
            request.ProfessionalId,
            ct
        );
        var activeSessions = await rooms.CountActiveSessionsAsync(request.ProfessionalId, ct);
        var alertsUnread = await alerts.CountUnreadAsync(request.UserId, ct);

        return new AdminSummaryDto(
            appointmentsToday,
            appointmentsPending,
            appointmentsCompleted,
            requestsPending,
            activeSessions,
            alertsUnread
        );
    }
}

/// <summary>
/// Listado de solicitudes del profesional autenticado, paginado y con filtros
/// (estado, paciente, rango). Es la variante "mis solicitudes" del listado
/// administrativo: el <c>ProfessionalId</c> sale de la identidad del JWT en el
/// controlador, nunca de un id enviado por el cliente — un profesional solo ve
/// las solicitudes que los pacientes enviaron a su agenda. Reutiliza el shape
/// del listado admin para que la UI sea idéntica cambiando solo el origen.
/// </summary>
public sealed record ListMyRequestsQuery(
    Guid ProfessionalId,
    AppointmentRequestStatus? Status,
    Guid? PatientId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedAdminRequestsResult>;

public sealed class ListMyRequestsQueryHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData
) : IRequestHandler<ListMyRequestsQuery, PaginatedAdminRequestsResult>
{
    public async Task<PaginatedAdminRequestsResult> Handle(
        ListMyRequestsQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Alcance forzado por identidad: el professionalId del query es el del
        // JWT, no un filtro opcional del cliente.
        var (items, total) = await requests.ListAdminAsync(
            request.Status,
            request.ProfessionalId,
            request.PatientId,
            request.From?.ToUniversalTime(),
            request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct
        );

        var dtos = await RequestDtos.BuildAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminRequestsResult(dtos, total, page, pageSize, totalPages);
    }
}
