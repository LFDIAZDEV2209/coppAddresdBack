using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del agregado cita. Los conflictos de concurrencia del
/// agendamiento (exclusión de solapamiento / índice único parcial) llegan como
/// <see cref="DbUpdateException"/> que envuelve la excepción de PostgreSQL y se
/// traducen a <see cref="BusinessRuleViolationException"/> para un error amigable
/// (409).
/// </summary>
public sealed class AppointmentRepository(TelemedicineDbContext dbContext) : IAppointmentRepository
{
    /// <summary>
    /// Ids de las sesiones cargadas de BD en <see cref="GetForUpdateAsync"/> (vía
    /// el Include de la sala). Distingue una sesión NUEVA (agregada al agregado)
    /// de una cargada que transiciona de estado (Active → Ended).
    /// </summary>
    private readonly HashSet<Guid> _loadedSessionIds = [];

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        var appointment = await dbContext
            .Appointments.Include(a => a.Cancellations)
            .Include(a => a.Reschedules)
            .Include(a => a.Request)
            .Include(a => a.Room)
                .ThenInclude(r => r!.Sessions)
            .Include(a => a.Encounter)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        // El fixup de EF puebla appointment.Sessions desde la sala cargada: las
        // sesiones conocidas son las de la sala (referencia para el estado Added).
        if (appointment?.Room is not null)
        {
            _loadedSessionIds.UnionWith(appointment.Room.Sessions.Select(s => s.Id));
        }

        return appointment;
    }

    public async Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default)
    {
        dbContext.Appointments.Add(appointment);
        await SaveWithConflictTranslationAsync(ct);
        return appointment;
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
        // Historial append-only (cancelaciones/reprogramaciones): los hijos ya
        // existentes cargados por GetForUpdateAsync están Unchanged; cualquier
        // otro estado es un hijo nuevo que EF marcó como Modified (no Added)
        // porque el agregado ya estaba tracked. Se fuerza Added para que se
        // INSERTE en lugar de intentar un UPDATE contra una fila inexistente.
        foreach (var cancellation in appointment.Cancellations)
        {
            if (dbContext.Entry(cancellation).State != EntityState.Unchanged)
            {
                dbContext.Entry(cancellation).State = EntityState.Added;
            }
        }

        foreach (var reschedule in appointment.Reschedules)
        {
            if (dbContext.Entry(reschedule).State != EntityState.Unchanged)
            {
                dbContext.Entry(reschedule).State = EntityState.Added;
            }
        }

        // NOTA: las sesiones NUEVAS se re-trackean con DbSet.Add. El fixup de EF
        // (RoomId → sala cargada con ThenInclude Sessions) marca una sesión
        // agregada a la colección del agregado como Modified — por su clave Guid
        // no generada por BD, el fixup asume que ya existe — lo que genera un
        // UPDATE contra una fila inexistente (DbUpdateConcurrency → 409).
        // DbSet.Add sobrevive a DetectChanges; Entry.State = Added no (el fixup
        // lo re-marca). Las sesiones cargadas de BD conservan su estado para la
        // transición Active → Ended.
        foreach (var session in appointment.Sessions)
        {
            if (!_loadedSessionIds.Contains(session.Id))
            {
                dbContext.Sessions.Add(session);
            }
        }

        await SaveWithConflictTranslationAsync(ct);
    }

    public async Task<bool> HasActiveOverlapAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId = null,
        CancellationToken ct = default
    ) =>
        await dbContext.Appointments.AnyAsync(
            a =>
                a.ProfessionalId == professionalId
                // Estados que ocupan el calendario (mismo criterio que el índice parcial).
                // Inline de la condición: EF no traduce helpers de método.
                && (
                    a.Status == AppointmentStatus.Requested
                    || a.Status == AppointmentStatus.Confirmed
                    || a.Status == AppointmentStatus.InProgress
                )
                && a.ScheduledStart < end
                && a.ScheduledEnd > start
                && (excludeAppointmentId == null || a.Id != excludeAppointmentId),
            ct
        );

    public async Task<IReadOnlyList<Appointment>> ListByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Appointments.AsNoTracking()
            .Where(a =>
                a.ProfessionalId == professionalId
                && a.ScheduledStart >= from
                && a.ScheduledStart < to
            )
            .OrderBy(a => a.ScheduledStart)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledStart)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Appointment> Items, int Total)> ListAdminAsync(
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
        var query = dbContext.Appointments.AsNoTracking();

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

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.ScheduledStart)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        await dbContext.Appointments.CountAsync(
            a => a.ScheduledStart >= from && a.ScheduledStart < to,
            ct
        );

    /// <summary>
    /// Último día de agenda cubierto por un rango <c>[from, to)</c>: el
    /// <c>to</c> es exclusivo, así que si cae justo a medianoche su día queda
    /// fuera (misma semántica que la rama directa con
    /// <c>scheduled_start &lt; to</c>). Sin esto, el pre-agregado sumaría un día
    /// completo de más en los bordes (p. ej. "hoy" incluiría mañana).
    /// </summary>
    private static DateOnly InclusiveEndDate(DateTimeOffset to)
    {
        var utc = to.UtcDateTime;
        var toDate = DateOnly.FromDateTime(utc);
        return utc.TimeOfDay == TimeSpan.Zero ? toDate.AddDays(-1) : toDate;
    }

    public async Task<int> CountInRangeAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default,
        bool usePreagg = true
    )
    {
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = InclusiveEndDate(to);
        var profId = professionalId ?? Guid.Empty;

        long? preaggTotal = null;
        if (usePreagg)
        {
            preaggTotal = await dbContext
                .AppointmentDailyMetrics.AsNoTracking()
                .Where(m =>
                    m.ProfessionalId == profId
                    && m.MetricDate >= fromDate
                    && m.MetricDate <= toDate
                    && m.MetricKey == "daily_total"
                )
                .SumAsync(m => (long?)m.TotalCount, ct);
        }

        if (preaggTotal.HasValue && preaggTotal.Value > 0)
        {
            return (int)preaggTotal.Value;
        }

        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        return await query.CountAsync(ct);
    }

    public async Task<int> CountByStatusAsync(
        AppointmentStatus status,
        CancellationToken ct = default
    ) => await dbContext.Appointments.CountAsync(a => a.Status == status, ct);

    public async Task<int> CountByStatusAsync(
        AppointmentStatus status,
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Appointments.AsNoTracking()
            .CountAsync(a => a.Status == status && a.ProfessionalId == professionalId, ct);

    // --- Analytics del dashboard (agrupaciones en BD, sin traer entidades) ---

    public async Task<IReadOnlyList<DailyAppointmentCount>> CountGroupedByDayAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = InclusiveEndDate(to);
        var profId = professionalId ?? Guid.Empty;

        var preagg = await dbContext
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m =>
                m.ProfessionalId == profId
                && m.MetricDate >= fromDate
                && m.MetricDate <= toDate
                && m.MetricKey == "daily_total"
            )
            .ToListAsync(ct);

        if (preagg.Count > 0)
        {
            return preagg
                .Select(m => new DailyAppointmentCount(
                    new DateTimeOffset(m.MetricDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    (int)m.TotalCount
                ))
                .OrderBy(x => x.Day)
                .ToList();
        }

        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        // Npgsql no traduce DateTimeOffset.Date en GroupBy (date_trunc no está
        // expuesto en EF.Functions 10); se proyectan solo los inicios y se agrupa
        // por día en memoria. El rango del dashboard es acotado (30-90 días), así
        // que la fila proyectada es ligera (una columna).
        var starts = await query.Select(a => a.ScheduledStart).ToListAsync(ct);

        return starts
            .GroupBy(s => new DateTimeOffset(s.Year, s.Month, s.Day, 0, 0, 0, s.Offset))
            .Select(g => new DailyAppointmentCount(g.Key, g.Count()))
            .OrderBy(x => x.Day)
            .ToList();
    }

    public async Task<IReadOnlyList<AppointmentStatusCount>> CountGroupedByStatusAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = InclusiveEndDate(to);
        var profId = professionalId ?? Guid.Empty;

        var preagg = await dbContext
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m =>
                m.ProfessionalId == profId
                && m.MetricDate >= fromDate
                && m.MetricDate <= toDate
                && m.MetricKey == "status_count"
            )
            .ToListAsync(ct);

        if (preagg.Count > 0)
        {
            return preagg
                .GroupBy(m => m.DimensionKey)
                .Select(g => new { StatusStr = g.Key, Count = (int)g.Sum(x => x.TotalCount) })
                .Where(x => Enum.TryParse<AppointmentStatus>(x.StatusStr, out _))
                .Select(x => new AppointmentStatusCount(
                    Enum.Parse<AppointmentStatus>(x.StatusStr),
                    x.Count
                ))
                .OrderBy(x => x.Status)
                .ToList();
        }

        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        // Los enums se persisten como string y Npgsql no traduce GroupBy sobre
        // ellos (mismo patrón que el agrupado por día): proyección ligera del
        // estado y agrupación en memoria (rango acotado del dashboard).
        var statuses = await query.Select(a => a.Status).ToListAsync(ct);

        return statuses
            .GroupBy(s => s)
            .Select(g => new AppointmentStatusCount(g.Key, g.Count()))
            .OrderBy(x => x.Status)
            .ToList();
    }

    public async Task<IReadOnlyList<HourlyAppointmentCount>> CountGroupedByHourAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = InclusiveEndDate(to);
        var profId = professionalId ?? Guid.Empty;

        var preagg = await dbContext
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m =>
                m.ProfessionalId == profId
                && m.MetricDate >= fromDate
                && m.MetricDate <= toDate
                && m.MetricKey == "hourly_count"
            )
            .ToListAsync(ct);

        if (preagg.Count > 0)
        {
            return preagg
                .GroupBy(m => m.DimensionKey)
                .Select(g =>
                {
                    var raw = g.Key.Replace("Hour_", "");
                    var hour = int.TryParse(raw, out var h) ? h : 0;
                    return new HourlyAppointmentCount(hour, (int)g.Sum(x => x.TotalCount));
                })
                .OrderBy(x => x.Hour)
                .ToList();
        }

        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        var starts = await query.Select(a => a.ScheduledStart).ToListAsync(ct);

        return starts
            .GroupBy(s => s.Hour)
            .Select(g => new HourlyAppointmentCount(g.Key, g.Count()))
            .OrderBy(x => x.Hour)
            .ToList();
    }

    public async Task<
        IReadOnlyList<ProfessionalAppointmentActivity>
    > CountGroupedByProfessionalAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var fromDate = DateOnly.FromDateTime(from.UtcDateTime);
        var toDate = InclusiveEndDate(to);

        var stats = await dbContext
            .ProfessionalDailyStats.AsNoTracking()
            .Where(s => s.MetricDate >= fromDate && s.MetricDate <= toDate)
            .ToListAsync(ct);

        if (stats.Count > 0)
        {
            return stats
                .GroupBy(s => s.ProfessionalId)
                .Select(g => new ProfessionalAppointmentActivity(
                    g.Key,
                    g.Sum(x => x.TotalAppointments),
                    g.Sum(x => x.CompletedAppointments),
                    g.Sum(x => x.CancelledAppointments),
                    g.Sum(x => x.UniquePatients)
                ))
                .OrderByDescending(x => x.Total)
                .ToList();
        }

        var rows = await dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to)
            .Select(a => new
            {
                a.ProfessionalId,
                a.PatientId,
                a.Status,
            })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.ProfessionalId)
            .Select(g => new ProfessionalAppointmentActivity(
                g.Key,
                g.Count(),
                g.Count(r => r.Status == AppointmentStatus.Completed),
                g.Count(r => r.Status == AppointmentStatus.Cancelled),
                g.Select(r => r.PatientId).Distinct().Count()
            ))
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    public async Task<int> CountDistinctPatientsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        return await query.Select(a => a.PatientId).Distinct().CountAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListPatientIdsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        return await query.Select(a => a.PatientId).ToListAsync(ct);
    }

    public async Task<int> CountDistinctProfessionalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Appointments.AsNoTracking()
            .Where(a => a.ScheduledStart >= from && a.ScheduledStart < to)
            .Select(a => a.ProfessionalId)
            .Distinct()
            .CountAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListUpcomingAsync(
        Guid? professionalId,
        DateTimeOffset from,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Appointments.AsNoTracking().Where(a => a.ScheduledStart >= from);

        if (professionalId is not null)
        {
            query = query.Where(a => a.ProfessionalId == professionalId);
        }

        return await query.OrderBy(a => a.ScheduledStart).Take(limit).ToListAsync(ct);
    }

    private async Task SaveWithConflictTranslationAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleViolationException(
                "La cita cambió de estado en otra operación concurrente; reintenta la operación."
            );
        }
        catch (DbUpdateException ex) when (ex.IsExclusionViolation() || ex.IsUniqueViolation())
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita que se solapa con el horario solicitado, o la solicitud ya fue confirmada."
            );
        }
    }
}
