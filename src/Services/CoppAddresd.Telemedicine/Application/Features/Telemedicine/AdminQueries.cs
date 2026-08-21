using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Listado administrativo de sesiones de video (vista "Sesiones" del admin).</summary>
public sealed record ListAdminSessionsQuery(
    Guid? AppointmentId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedAdminSessionsResult>;

public sealed record PaginatedAdminSessionsResult(
    IReadOnlyList<TelemedicineSessionDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record TelemedicineSessionDto(
    Guid Id,
    Guid AppointmentId,
    Guid? PatientId,
    string? PatientName,
    Guid? ProfessionalId,
    string? ProfessionalName,
    TelemedicineSessionStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationSeconds,
    string? EndReason,
    DateTime CreatedAt);

public sealed class ListAdminSessionsQueryHandler(
    IRoomRepository rooms,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<ListAdminSessionsQuery, PaginatedAdminSessionsResult>
{
    public async Task<PaginatedAdminSessionsResult> Handle(
        ListAdminSessionsQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await rooms.ListSessionsAsync(
            request.AppointmentId,
            request.From?.ToUniversalTime(),
            request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct);

        var dtos = await SessionDtos.BuildAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminSessionsResult(dtos, total, page, pageSize, totalPages);
    }
}

/// <summary>Constructor de DTOs de sesión con nombres de paciente/profesional resueltos (deduplicados).</summary>
internal static class SessionDtos
{
    public static async Task<IReadOnlyList<TelemedicineSessionDto>> BuildAsync(
        IReadOnlyList<TelemedicineSession> items,
        ITelemedicineReferenceDataService referenceData,
        CancellationToken ct)
    {
        var patientIds = items
            .Select(s => s.Appointment?.PatientId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var professionalIds = items
            .Select(s => s.Appointment?.ProfessionalId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var patients = new Dictionary<Guid, PatientRefDto>();
        var professionals = new Dictionary<Guid, ProfessionalRefDto>();

        foreach (var id in patientIds)
        {
            if (await referenceData.GetPatientAsync(id, ct) is { } p)
            {
                patients[id] = p;
            }
        }

        foreach (var id in professionalIds)
        {
            if (await referenceData.GetProfessionalAsync(id, ct) is { } pr)
            {
                professionals[id] = pr;
            }
        }

        return items
            .Select(s => new TelemedicineSessionDto(
                s.Id,
                s.AppointmentId,
                s.Appointment?.PatientId,
                s.Appointment is { } a ? patients.GetValueOrDefault(a.PatientId)?.FullName : null,
                s.Appointment?.ProfessionalId,
                s.Appointment is { } appt ? professionals.GetValueOrDefault(appt.ProfessionalId)?.FullName : null,
                s.Status,
                s.StartedAt,
                s.EndedAt,
                s.DurationSeconds,
                s.EndReason,
                s.CreatedAt))
            .ToList();
    }
}

/// <summary>KPIs del dashboard administrativo (resumen operativo de Telemedicina).</summary>
public sealed record AdminSummaryDto(
    int AppointmentsToday,
    int AppointmentsPending,
    int AppointmentsCompleted,
    int RequestsPending,
    int ActiveSessions,
    int AlertsUnread);

/// <summary>Consulta los KPIs del dashboard admin (una sola ronda de conteos).</summary>
public sealed record GetAdminSummaryQuery(Guid? ClinicId) : IRequest<AdminSummaryDto>;

public sealed class GetAdminSummaryQueryHandler(
    IAppointmentRepository appointments,
    IRequestRepository requests,
    IAlertRepository alerts,
    IRoomRepository rooms)
    : IRequestHandler<GetAdminSummaryQuery, AdminSummaryDto>
{
    public async Task<AdminSummaryDto> Handle(GetAdminSummaryQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1);

        // Conteos dirigidos (sin traer entidades): una consulta por métrica.
        var (appointmentsToday, appointmentsPending, appointmentsCompleted) = await CountAppointmentsAsync(ct);
        var requestsPending = await requests.CountByStatusAsync(AppointmentRequestStatus.Pending, ct);
        var activeSessions = await rooms.CountActiveSessionsAsync(ct);
        var alertsUnread = await alerts.CountUnreadGlobalAsync(ct);

        return new AdminSummaryDto(
            appointmentsToday,
            appointmentsPending,
            appointmentsCompleted,
            requestsPending,
            activeSessions,
            alertsUnread);

        async Task<(int Today, int Pending, int Completed)> CountAppointmentsAsync(CancellationToken c)
        {
            var today = await appointments.CountInRangeAsync(startOfDay, endOfDay, c);
            var pending = await appointments.CountByStatusAsync(AppointmentStatus.Confirmed, c);
            var completed = await appointments.CountByStatusAsync(AppointmentStatus.Completed, c);
            return (today, pending, completed);
        }
    }
}

/// <summary>
/// Contexto del usuario autenticado (JWT) para la UI: resuelve el profesional y/o
/// el paciente que corresponden al userId del token, usando los datos de
/// referencia del ERP. El frontend necesita el <c>professionalId</c> para "mi
/// agenda"/"mis solicitudes" y el dashboard; nunca debe adivinar su id.
/// </summary>
public sealed record GetCurrentUserContextQuery(Guid UserId) : IRequest<CurrentUserContextDto>;

public sealed record CurrentUserContextDto(
    ProfessionalRefDto? Professional,
    PatientRefDto? Patient);

public sealed class GetCurrentUserContextQueryHandler(
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<GetCurrentUserContextQuery, CurrentUserContextDto>
{
    public async Task<CurrentUserContextDto> Handle(
        GetCurrentUserContextQuery request,
        CancellationToken ct)
    {
        var professional = await referenceData.GetProfessionalByUserIdAsync(request.UserId, ct);
        var patient = await referenceData.GetPatientByUserIdAsync(request.UserId, ct);
        return new CurrentUserContextDto(professional, patient);
    }
}

/// <summary>Listado administrativo global de citas (vista "Citas" del admin), paginado y con filtros.</summary>
public sealed record ListAdminAppointmentsQuery(
    Guid? ProfessionalId,
    Guid? PatientId,
    Guid? ClinicId,
    Guid? LocationId,
    AppointmentStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedAdminAppointmentsResult>;

public sealed record PaginatedAdminAppointmentsResult(
    IReadOnlyList<TelemedicineAppointmentDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class ListAdminAppointmentsQueryHandler(
    IAppointmentRepository appointments,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<ListAdminAppointmentsQuery, PaginatedAdminAppointmentsResult>
{
    public async Task<PaginatedAdminAppointmentsResult> Handle(
        ListAdminAppointmentsQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await appointments.ListAdminAsync(
            request.ProfessionalId,
            request.PatientId,
            request.ClinicId,
            request.LocationId,
            request.Status,
            request.From?.ToUniversalTime(),
            request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct);

        var dtos = await TelemedicineAppointmentMapper.BuildDtosAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminAppointmentsResult(dtos, total, page, pageSize, totalPages);
    }
}

/// <summary>
/// Listado global de solicitudes (admin) o de un profesional concreto
/// (<c>ProfessionalId</c> = su bandeja de solicitudes a confirmar), paginado.
/// </summary>
public sealed record ListAdminRequestsQuery(
    AppointmentRequestStatus? Status,
    Guid? ProfessionalId,
    Guid? PatientId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedAdminRequestsResult>;

public sealed record PaginatedAdminRequestsResult(
    IReadOnlyList<TelemedicineRequestDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class ListAdminRequestsQueryHandler(
    IRequestRepository requests,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<ListAdminRequestsQuery, PaginatedAdminRequestsResult>
{
    public async Task<PaginatedAdminRequestsResult> Handle(
        ListAdminRequestsQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await requests.ListAdminAsync(
            request.Status,
            request.ProfessionalId,
            request.PatientId,
            request.From?.ToUniversalTime(),
            request.To?.ToUniversalTime(),
            page,
            pageSize,
            ct);

        var dtos = await RequestDtos.BuildAsync(items, referenceData, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdminRequestsResult(dtos, total, page, pageSize, totalPages);
    }
}

/// <summary>Constructor de DTOs de solicitud con nombres resueltos y deduplicados (sin N+1).</summary>
internal static class RequestDtos
{
    public static async Task<IReadOnlyList<TelemedicineRequestDto>> BuildAsync(
        IReadOnlyList<TelemedicineRequest> items,
        ITelemedicineReferenceDataService referenceData,
        CancellationToken ct)
    {
        var patientIds = items.Select(r => r.PatientId).Distinct().ToList();
        var specialtyIds = items.Select(r => r.SpecialtyId).Distinct().ToList();

        var patients = new Dictionary<Guid, PatientRefDto>();
        var specialties = new Dictionary<Guid, SpecialtyRefDto>();

        foreach (var id in patientIds)
        {
            if (await referenceData.GetPatientAsync(id, ct) is { } p)
            {
                patients[id] = p;
            }
        }

        foreach (var id in specialtyIds)
        {
            if (await referenceData.GetSpecialtyAsync(id, ct) is { } s)
            {
                specialties[id] = s;
            }
        }

        return items
            .Select(r => new TelemedicineRequestDto(
                r.Id,
                r.PatientId,
                patients.GetValueOrDefault(r.PatientId)?.FullName,
                r.ProfessionalId,
                r.SpecialtyId,
                specialties.GetValueOrDefault(r.SpecialtyId)?.Name,
                r.OrganizationId,
                r.ClinicId,
                r.LocationId,
                r.PreferredStart,
                r.Reason,
                r.Status,
                r.CreatedAt))
            .ToList();
    }
}
