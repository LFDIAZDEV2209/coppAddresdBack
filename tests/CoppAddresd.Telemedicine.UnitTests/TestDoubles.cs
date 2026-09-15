using CoppAddresd.Telemedicine.Application.Configuration;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Dobles de prueba del microservicio de Telemedicina: implementaciones en
/// memoria de las interfaces de Application/Infrastructure (mismo estilo de
/// fakes escritos a mano del proyecto). Soportan la suite unitaria de handlers
/// y reglas de negocio sin base de datos ni proveedor real.
/// </summary>
public static class TestData
{
    public static Guid Org = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static Guid Clinic = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static Guid PatientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static Guid ProfessionalId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static Guid SpecialtyId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static Guid LocationId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    public static Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static Guid PatientUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static ProfessionalRefDto Professional(
        Guid? id = null,
        Guid? userId = null,
        IReadOnlyList<Guid>? specialtyIds = null
    ) =>
        new(
            id ?? ProfessionalId,
            Guid.NewGuid(),
            userId,
            "Dra. Ana Pérez",
            "Physician",
            specialtyIds ?? [SpecialtyId],
            [LocationId],
            [Clinic]
        );

    public static PatientRefDto Patient(Guid? id = null) =>
        new(id ?? PatientId, "María Gómez", "maria@x.com", Clinic, LocationId);

    public static SpecialtyRefDto Specialty(Guid? id = null) =>
        new(id ?? SpecialtyId, "MED-GEN", "Medicina General", "General");

    public static LocationRefDto Location(Guid? id = null) =>
        new(id ?? LocationId, "Sede Principal", Clinic, true);

    public static TelemedicineSettings Settings(
        Guid? org = null,
        Guid? clinic = null,
        int minAdvanceHours = 2,
        int maxAdvanceDays = 30,
        int maxReschedules = 2
    ) =>
        new()
        {
            OrganizationId = org ?? Org,
            ClinicId = clinic,
            MinAdvanceBookingHours = minAdvanceHours,
            MaxAdvanceBookingDays = maxAdvanceDays,
            MaxReschedules = maxReschedules,
            DefaultAppointmentDurationMinutes = 30,
            RoomOpenBeforeMinutes = 10,
            RoomCloseAfterMinutes = 15,
            AccessTokenTtlSeconds = 900,
            MaxParticipants = 2,
        };

    public static Appointment Appointment(
        AppointmentStatus status = AppointmentStatus.Confirmed,
        Guid? professionalId = null,
        Guid? patientId = null,
        Guid? locationId = null,
        DateTimeOffset? start = null,
        int rescheduleCount = 0
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            RequestId = null,
            PatientId = patientId ?? PatientId,
            ProfessionalId = professionalId ?? ProfessionalId,
            SpecialtyId = SpecialtyId,
            OrganizationId = Org,
            ClinicId = Clinic,
            LocationId = locationId ?? LocationId,
            ScheduledStart = start ?? DateTimeOffset.UtcNow.AddHours(5),
            ScheduledEnd = (start ?? DateTimeOffset.UtcNow.AddHours(5)).AddMinutes(30),
            DurationMinutes = 30,
            Status = status,
            RescheduleCount = rescheduleCount,
            CreatedBy = UserId,
        };
}

/// <summary>Datos de referencia del ERP en memoria (sustituye al AppointmentReferenceDataService).</summary>
public sealed class FakeReferenceDataService : IAppointmentReferenceDataService
{
    public Dictionary<Guid, ProfessionalRefDto> Professionals { get; } = [];
    public Dictionary<Guid, PatientRefDto> Patients { get; } = [];
    public Dictionary<Guid, SpecialtyRefDto> Specialties { get; } = [];
    public Dictionary<Guid, LocationRefDto> Locations { get; } = [];
    public Dictionary<Guid, Guid> UserToProfessional { get; } = [];
    public Dictionary<Guid, Guid> UserToPatient { get; } = [];

    public Task<ProfessionalRefDto?> GetProfessionalAsync(
        Guid professionalId,
        CancellationToken ct = default
    ) => Task.FromResult(Professionals.GetValueOrDefault(professionalId));

    public Task<PatientRefDto?> GetPatientAsync(Guid patientId, CancellationToken ct = default) =>
        Task.FromResult(Patients.GetValueOrDefault(patientId));

    public Task<SpecialtyRefDto?> GetSpecialtyAsync(
        Guid specialtyId,
        CancellationToken ct = default
    ) => Task.FromResult(Specialties.GetValueOrDefault(specialtyId));

    public Task<LocationRefDto?> GetLocationAsync(
        Guid locationId,
        CancellationToken ct = default
    ) => Task.FromResult(Locations.GetValueOrDefault(locationId));

    public Task<ProfessionalRefDto?> GetProfessionalByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            UserToProfessional.TryGetValue(userId, out var id)
                ? Professionals.GetValueOrDefault(id)
                : null
        );

    public Task<PatientRefDto?> GetPatientByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            UserToPatient.TryGetValue(userId, out var id) ? Patients.GetValueOrDefault(id) : null
        );
}

/// <summary>Proveedor de settings en memoria (sustituye al TelemedicineSettingsProvider).</summary>
public sealed class FakeSettingsProvider : ITelemedicineSettingsProvider
{
    public TelemedicineSettings Settings { get; set; } = TestData.Settings();
    public int Calls { get; private set; }

    public Task<TelemedicineSettings> GetSettingsAsync(
        Guid organizationId,
        Guid? clinicId,
        CancellationToken ct = default
    )
    {
        Calls++;
        return Task.FromResult(Settings);
    }
}

/// <summary>Proveedor de video configurable (sustituye al TwilioVideoProvider).</summary>
public sealed class FakeVideoProvider : IVideoProvider
{
    public bool SignatureValid { get; set; } = true;
    public bool CompleteRoomThrows { get; set; }
    public int CreateRoomCalls { get; private set; }
    public int CompleteRoomCalls { get; private set; }

    public Task<RoomInfo> CreateRoomAsync(RoomRequest request, CancellationToken ct)
    {
        CreateRoomCalls++;
        return Task.FromResult(
            new RoomInfo(
                $"RM{CreateRoomCalls}",
                request.RoomName,
                "in-progress",
                DateTimeOffset.UtcNow,
                null,
                null
            )
        );
    }

    public Task<RoomInfo?> GetRoomAsync(string providerRoomSidOrName, CancellationToken ct) =>
        Task.FromResult<RoomInfo?>(null);

    public Task CompleteRoomAsync(string providerRoomSid, CancellationToken ct)
    {
        CompleteRoomCalls++;
        if (CompleteRoomThrows)
        {
            throw new InvalidOperationException("Proveedor no disponible.");
        }
        return Task.CompletedTask;
    }

    public Task<string> GenerateAccessTokenAsync(
        AccessTokenRequest request,
        CancellationToken ct
    ) => Task.FromResult("fake-access-token");

    public Task<IReadOnlyList<ParticipantInfo>> GetParticipantsAsync(
        string providerRoomSid,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyList<ParticipantInfo>>([]);

    public Task<bool> ValidateWebhookSignatureAsync(
        WebhookValidationRequest request,
        CancellationToken ct
    ) => Task.FromResult(SignatureValid);
}

/// <summary>Unidad de trabajo que ejecuta la acción directamente (sin transacción real).</summary>
public sealed class FakeUnitOfWork : ITelemedicineUnitOfWork
{
    public int Calls { get; private set; }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct = default
    )
    {
        Calls++;
        return action(ct);
    }
}

/// <summary>Repositorio de citas en memoria.</summary>
public sealed class FakeAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Items { get; } = [];

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default)
    {
        Items.Add(appointment);
        return Task.FromResult(appointment);
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<bool> HasActiveOverlapAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId = null,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Items.Any(a =>
                a.ProfessionalId == professionalId
                && a.Status
                    is AppointmentStatus.Requested
                        or AppointmentStatus.Confirmed
                        or AppointmentStatus.InProgress
                && a.ScheduledStart < end
                && a.ScheduledEnd > start
                && (excludeAppointmentId == null || a.Id != excludeAppointmentId)
            )
        );

    public Task<IReadOnlyList<Appointment>> ListByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<Appointment>>(
            Items
                .Where(a =>
                    a.ProfessionalId == professionalId
                    && a.ScheduledStart >= from
                    && a.ScheduledStart < to
                )
                .OrderBy(a => a.ScheduledStart)
                .ToList()
        );

    public Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<Appointment>>(
            Items
                .Where(a => a.PatientId == patientId)
                .OrderByDescending(a => a.ScheduledStart)
                .ToList()
        );

    public Task<(IReadOnlyList<Appointment> Items, int Total)> ListAdminAsync(
        Guid? professionalId,
        Guid? patientId,
        Guid? clinicId,
        Guid? locationId,
        AppointmentStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Items.AsEnumerable();
        if (professionalId is not null)
            query = query.Where(a => a.ProfessionalId == professionalId);
        if (patientId is not null)
            query = query.Where(a => a.PatientId == patientId);
        if (clinicId is not null)
            query = query.Where(a => a.ClinicId == clinicId);
        if (locationId is not null)
            query = query.Where(a => a.LocationId == locationId);
        if (status is not null)
            query = query.Where(a => a.Status == status);
        if (from is not null)
            query = query.Where(a => a.ScheduledStart >= from);
        if (to is not null)
            query = query.Where(a => a.ScheduledStart < to);

        var list = query.OrderByDescending(a => a.ScheduledStart).ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<Appointment>,
                list.Count
            )
        );
    }

    public Task<int> CountInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) => Task.FromResult(Items.Count(a => a.ScheduledStart >= from && a.ScheduledStart < to));

    public Task<int> CountInRangeAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default,
        bool usePreagg = true
    ) =>
        Task.FromResult(
            Items.Count(a =>
                (professionalId == null || a.ProfessionalId == professionalId)
                && a.ScheduledStart >= from
                && a.ScheduledStart < to
            )
        );

    public Task<int> CountByStatusAsync(AppointmentStatus status, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(a => a.Status == status));

    public Task<int> CountByStatusAsync(
        AppointmentStatus status,
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(Items.Count(a => a.Status == status && a.ProfessionalId == professionalId));

    public Task<IReadOnlyList<DailyAppointmentCount>> CountGroupedByDayAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<DailyAppointmentCount>>(
            Items
                .Where(a =>
                    (professionalId == null || a.ProfessionalId == professionalId)
                    && a.ScheduledStart >= from
                    && a.ScheduledStart < to
                )
                .GroupBy(a => a.ScheduledStart.Date)
                .Select(g => new DailyAppointmentCount(g.Key, g.Count()))
                .OrderBy(x => x.Day)
                .ToList()
        );

    public Task<IReadOnlyList<AppointmentStatusCount>> CountGroupedByStatusAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<AppointmentStatusCount>>(
            Items
                .Where(a =>
                    (professionalId == null || a.ProfessionalId == professionalId)
                    && a.ScheduledStart >= from
                    && a.ScheduledStart < to
                )
                .GroupBy(a => a.Status)
                .Select(g => new AppointmentStatusCount(g.Key, g.Count()))
                .OrderBy(x => x.Status)
                .ToList()
        );

    public Task<IReadOnlyList<HourlyAppointmentCount>> CountGroupedByHourAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<HourlyAppointmentCount>>(
            Items
                .Where(a =>
                    (professionalId == null || a.ProfessionalId == professionalId)
                    && a.ScheduledStart >= from
                    && a.ScheduledStart < to
                )
                .GroupBy(a => a.ScheduledStart.Hour)
                .Select(g => new HourlyAppointmentCount(g.Key, g.Count()))
                .OrderBy(x => x.Hour)
                .ToList()
        );

    public Task<IReadOnlyList<ProfessionalAppointmentActivity>> CountGroupedByProfessionalAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProfessionalAppointmentActivity>>(
            Items
                .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to)
                .GroupBy(a => a.ProfessionalId)
                .Select(g => new ProfessionalAppointmentActivity(
                    g.Key,
                    g.Count(),
                    g.Count(a => a.Status == AppointmentStatus.Completed),
                    g.Count(a => a.Status == AppointmentStatus.Cancelled),
                    g.Select(a => a.PatientId).Distinct().Count()
                ))
                .OrderByDescending(x => x.Total)
                .ToList()
        );

    public Task<int> CountDistinctPatientsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Items
                .Where(a =>
                    (professionalId == null || a.ProfessionalId == professionalId)
                    && a.ScheduledStart >= from
                    && a.ScheduledStart < to
                )
                .Select(a => a.PatientId)
                .Distinct()
                .Count()
        );

    public Task<int> CountDistinctProfessionalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Items
                .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to)
                .Select(a => a.ProfessionalId)
                .Distinct()
                .Count()
        );

    public Task<IReadOnlyList<Appointment>> ListUpcomingAsync(
        Guid? professionalId,
        DateTimeOffset from,
        int limit,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<Appointment>>(
            Items
                .Where(a =>
                    (professionalId == null || a.ProfessionalId == professionalId)
                    && a.ScheduledStart >= from
                )
                .OrderBy(a => a.ScheduledStart)
                .Take(limit)
                .ToList()
        );
}

/// <summary>Repositorio de solicitudes en memoria.</summary>
public sealed class FakeRequestRepository : IRequestRepository
{
    public List<TelemedicineRequest> Items { get; } = [];

    public Task<TelemedicineRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<TelemedicineRequest> AddAsync(
        TelemedicineRequest request,
        CancellationToken ct = default
    )
    {
        Items.Add(request);
        return Task.FromResult(request);
    }

    public Task UpdateAsync(TelemedicineRequest request, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetStatusAsync(
        Guid requestId,
        AppointmentRequestStatus status,
        CancellationToken ct = default
    )
    {
        var item = Items.FirstOrDefault(r => r.Id == requestId);
        if (item is not null)
            item.Status = status;
        return Task.CompletedTask;
    }

    public Task SetRejectedAsync(Guid requestId, string reason, CancellationToken ct = default)
    {
        var item = Items.FirstOrDefault(r => r.Id == requestId);
        if (item is not null)
        {
            item.Status = AppointmentRequestStatus.Rejected;
            item.RejectionReason = reason;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelemedicineRequest>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<TelemedicineRequest>>(
            Items.Where(r => r.PatientId == patientId).OrderByDescending(r => r.CreatedAt).ToList()
        );

    public Task<(IReadOnlyList<TelemedicineRequest> Items, int Total)> ListByOrganizationAsync(
        Guid organizationId,
        AppointmentRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Items.Where(r => r.OrganizationId == organizationId);
        if (status is not null)
            query = query.Where(r => r.Status == status);
        var list = query.ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<TelemedicineRequest>,
                list.Count
            )
        );
    }

    public Task<(IReadOnlyList<TelemedicineRequest> Items, int Total)> ListAdminAsync(
        AppointmentRequestStatus? status,
        Guid? professionalId,
        Guid? patientId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Items.AsEnumerable();
        if (status is not null)
            query = query.Where(r => r.Status == status);
        if (professionalId is not null)
            query = query.Where(r => r.ProfessionalId == professionalId);
        if (patientId is not null)
            query = query.Where(r => r.PatientId == patientId);
        if (from is not null)
            query = query.Where(r => r.CreatedAt >= from);
        if (to is not null)
            query = query.Where(r => r.CreatedAt < to);
        var list = query.ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<TelemedicineRequest>,
                list.Count
            )
        );
    }

    public Task<int> CountByStatusAsync(
        AppointmentRequestStatus status,
        CancellationToken ct = default
    ) => Task.FromResult(Items.Count(r => r.Status == status));

    public Task<int> CountByStatusAsync(
        AppointmentRequestStatus status,
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(Items.Count(r => r.Status == status && r.ProfessionalId == professionalId));
}

/// <summary>Repositorio de salas/sesiones/webhooks en memoria.</summary>
public sealed class FakeRoomRepository : IRoomRepository
{
    public List<VirtualRoom> Rooms { get; } = [];
    public List<TelemedicineSession> Sessions { get; } = [];
    public List<TelemedicineWebhookEvent> WebhookEvents { get; } = [];

    public Task<VirtualRoom?> GetByAppointmentIdAsync(
        Guid appointmentId,
        bool includeSessions = false,
        CancellationToken ct = default
    ) => Task.FromResult(Rooms.FirstOrDefault(r => r.AppointmentId == appointmentId));

    public Task<VirtualRoom?> GetByProviderRoomSidAsync(
        string providerRoomSid,
        bool includeSessions = false,
        CancellationToken ct = default
    ) => Task.FromResult(Rooms.FirstOrDefault(r => r.ProviderRoomSid == providerRoomSid));

    public Task<VirtualRoom?> GetForUpdateAsync(
        Guid appointmentId,
        CancellationToken ct = default
    ) => Task.FromResult(Rooms.FirstOrDefault(r => r.AppointmentId == appointmentId));

    public Task<VirtualRoom?> GetForUpdateByProviderRoomSidAsync(
        string providerRoomSid,
        CancellationToken ct = default
    ) => Task.FromResult(Rooms.FirstOrDefault(r => r.ProviderRoomSid == providerRoomSid));

    public Task<VirtualRoom> AddAsync(VirtualRoom room, CancellationToken ct = default)
    {
        var existing = Rooms.FirstOrDefault(r => r.AppointmentId == room.AppointmentId);
        if (existing is not null)
            return Task.FromResult(existing);
        Rooms.Add(room);
        return Task.FromResult(room);
    }

    public Task UpdateAsync(VirtualRoom room, CancellationToken ct = default) => Task.CompletedTask;

    public Task AddWebhookEventAsync(
        TelemedicineWebhookEvent webhookEvent,
        CancellationToken ct = default
    )
    {
        if (
            WebhookEvents.Any(e =>
                e.EventType == webhookEvent.EventType
                && e.RoomSid == webhookEvent.RoomSid
                && e.ParticipantSid == webhookEvent.ParticipantSid
            )
        )
        {
            throw new BusinessRuleViolationException("El webhook del proveedor ya fue procesado.");
        }
        WebhookEvents.Add(webhookEvent);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<TelemedicineSession> Items, int Total)> ListSessionsAsync(
        Guid? appointmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Sessions.AsEnumerable();
        if (appointmentId is not null)
            query = query.Where(s => s.AppointmentId == appointmentId);
        var list = query.OrderByDescending(s => s.StartedAt ?? s.CreatedAt).ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<TelemedicineSession>,
                list.Count
            )
        );
    }

    public Task<int> CountActiveSessionsAsync(CancellationToken ct = default) =>
        Task.FromResult(Sessions.Count(s => s.Status == TelemedicineSessionStatus.Active));

    public Task<int> CountActiveSessionsAsync(
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Sessions.Count(s =>
                s.Status == TelemedicineSessionStatus.Active
                && s.Appointment?.ProfessionalId == professionalId
            )
        );
}

/// <summary>Repositorio de alertas en memoria.</summary>
public sealed class FakeAlertRepository : IAlertRepository
{
    public List<TelemedicineAlert> Items { get; } = [];

    public Task AddRangeAsync(
        IReadOnlyList<TelemedicineAlert> alerts,
        CancellationToken ct = default
    )
    {
        Items.AddRange(alerts);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListForUserAsync(
        Guid recipientUserId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Items.Where(a => a.RecipientUserId == recipientUserId);
        if (unreadOnly)
            query = query.Where(a => a.ReadAt == null);
        var list = query.OrderByDescending(a => a.CreatedAt).ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<TelemedicineAlert>,
                list.Count
            )
        );
    }

    public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(a => a.RecipientUserId == recipientUserId && a.ReadAt == null));

    public Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListAllAsync(
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = Items.AsEnumerable();
        if (unreadOnly)
            query = query.Where(a => a.ReadAt == null);
        var list = query.OrderByDescending(a => a.CreatedAt).ToList();
        return Task.FromResult(
            (
                list.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    as IReadOnlyList<TelemedicineAlert>,
                list.Count
            )
        );
    }

    public Task<bool> MarkReadByIdAsync(Guid alertId, CancellationToken ct = default)
    {
        var a = Items.FirstOrDefault(x => x.Id == alertId);
        if (a is null)
            return Task.FromResult(false);
        a.ReadAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<int> MarkAllReadGlobalAsync(CancellationToken ct = default)
    {
        var count = Items.Count(a => a.ReadAt == null);
        foreach (var a in Items.Where(a => a.ReadAt == null))
            a.ReadAt = DateTimeOffset.UtcNow;
        return Task.FromResult(count);
    }

    public Task<int> CountUnreadGlobalAsync(CancellationToken ct = default) =>
        Task.FromResult(Items.Count(a => a.ReadAt == null));

    public Task<bool> MarkReadAsync(
        Guid alertId,
        Guid recipientUserId,
        CancellationToken ct = default
    )
    {
        var a = Items.FirstOrDefault(x => x.Id == alertId && x.RecipientUserId == recipientUserId);
        if (a is null)
            return Task.FromResult(false);
        a.ReadAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default)
    {
        var targets = Items
            .Where(a => a.RecipientUserId == recipientUserId && a.ReadAt == null)
            .ToList();
        foreach (var a in targets)
            a.ReadAt = DateTimeOffset.UtcNow;
        return Task.FromResult(targets.Count);
    }
}

/// <summary>Repositorio de encuentros en memoria (idempotente por cita).</summary>
public sealed class FakeEncounterRepository : IEncounterRepository
{
    public List<ClinicalEncounter> Items { get; } = [];

    public Task<ClinicalEncounter?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default
    ) => Task.FromResult(Items.FirstOrDefault(e => e.AppointmentId == appointmentId));

    public Task<ClinicalEncounter?> GetForUpdateByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default
    ) => Task.FromResult(Items.FirstOrDefault(e => e.AppointmentId == appointmentId));

    public Task<ClinicalEncounter> AddAsync(
        ClinicalEncounter encounter,
        CancellationToken ct = default
    )
    {
        var existing = Items.FirstOrDefault(e => e.AppointmentId == encounter.AppointmentId);
        if (existing is not null)
            return Task.FromResult(existing);
        Items.Add(encounter);
        return Task.FromResult(encounter);
    }

    public Task UpdateAsync(ClinicalEncounter encounter, CancellationToken ct = default) =>
        Task.CompletedTask;
}

/// <summary>Opciones del microservicio (WebhookUrl) para los handlers.</summary>
public static class TestOptions
{
    public static IOptions<TelemedicineOptions> Create(
        string webhookUrl = "https://x/api/v1/telemedicine/webhooks/twilio"
    ) => Options.Create(new TelemedicineOptions { WebhookUrl = webhookUrl });
}
