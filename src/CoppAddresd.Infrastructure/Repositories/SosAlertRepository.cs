using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Implementación EF de la persistencia SOS sobre <c>app.sos_alerts</c> y el
/// outbox de dedupe (<c>app.notification_dedupe_keys</c>). La creación
/// transaccional discrimina violaciones de unicidad por CONSTRAINT para
/// resolver carreras sin reintentos: idempotencia → replay/409, índice
/// parcial de alerta activa → 409 con la existente.
/// </summary>
public sealed class SosAlertRepository(AppDbContext dbContext) : ISosAlertRepository
{
    private const string IdempotencyUniqueIndex = "uq_sos_alerts_patient_idempotency";
    private const string ActiveUniqueIndex = "uq_sos_alerts_patient_active";

    public Task<SosAlert?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        // Include del paciente: las plantillas server-side (SMS/voz) usan su
        // nombre de pila; sin el join caerían al fallback genérico.
        dbContext
            .SosAlerts.AsNoTracking()
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SosAlert?> GetByPatientAndKeyAsync(
        Guid patientId,
        string idempotencyKey,
        CancellationToken ct = default
    ) =>
        dbContext
            .SosAlerts.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId && x.IdempotencyKey == idempotencyKey,
                ct
            );

    public Task<SosAlert?> GetActiveByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        dbContext
            .SosAlerts.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.PatientId == patientId && x.Status == SosAlertStatus.Activa,
                ct
            );

    public Task<PatientProfile?> GetPatientProfileAsync(
        Guid patientId,
        CancellationToken ct = default
    ) => dbContext.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId, ct);

    public async Task<SosCreateOutcome> AddWithOutboxAsync(
        SosAlert alert,
        IReadOnlyCollection<string> dedupeKeys,
        CancellationToken ct = default
    )
    {
        dbContext.SosAlerts.Add(alert);
        foreach (var dedupeKey in dedupeKeys)
        {
            dbContext.NotificationDedupeKeys.Add(
                new NotificationDedupeKey
                {
                    Id = Guid.NewGuid(),
                    DedupeKey = dedupeKey,
                    // La fila de outbox del SMS no tiene destinatario auth.users
                    // (el destinatario es el contacto externo del perfil).
                    UserId = Guid.Empty,
                    // Solo el canal que representa la clave queda 'pendiente'
                    // (los otros canales de la fila son null, nunca varios).
                    SmsStatus = dedupeKey.StartsWith("sos:sms:", StringComparison.Ordinal)
                        ? SosChannelStatus.Pendiente.ToString().ToLowerInvariant()
                        : null,
                    VoiceStatus = dedupeKey.StartsWith("sos:voice:", StringComparison.Ordinal)
                        ? SosChannelStatus.Pendiente.ToString().ToLowerInvariant()
                        : null,
                    PushStatus =
                        dedupeKey.StartsWith("sos:sms:", StringComparison.Ordinal)
                        || dedupeKey.StartsWith("sos:voice:", StringComparison.Ordinal)
                            ? null
                            : SosChannelStatus.Pendiente.ToString().ToLowerInvariant(),
                }
            );
        }

        try
        {
            // Un único SaveChanges = UNA transacción: alerta + outbox
            // atómicos (D2). Nunca se envía un canal cuya fila de dedupe no
            // quedó comprometida.
            await dbContext.SaveChangesAsync(ct);
            return new SosCreateOutcome(SosCreateResult.Created, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            // Carrera: re-leer la fila conflictiva SIN rastreo para el mapeo
            // del handler (replay/409). La entidad fallida sale del tracker.
            dbContext.Entry(alert).State = EntityState.Detached;

            // PostgreSQL puede reportar CUALQUIERA de los dos índices cuando
            // ambos se violan a la vez (misma clave + activa existente): la
            // desambiguación real es por contenido, no por nombre de constraint.
            if (constraint == IdempotencyUniqueIndex)
            {
                var row = await GetByPatientAndKeyAsync(alert.PatientId, alert.IdempotencyKey, ct);
                return new SosCreateOutcome(SosCreateResult.IdempotencyCollision, row);
            }

            if (constraint == ActiveUniqueIndex)
            {
                // ¿La fila con esta (patient, key) ya existe? → colisión de
                // idempotencia disfrazada de colisión de índice parcial.
                var byKey = await GetByPatientAndKeyAsync(
                    alert.PatientId,
                    alert.IdempotencyKey,
                    ct
                );
                if (byKey is not null)
                {
                    return new SosCreateOutcome(SosCreateResult.IdempotencyCollision, byKey);
                }

                var active = await GetActiveByPatientAsync(alert.PatientId, ct);
                return new SosCreateOutcome(SosCreateResult.ActiveCollision, active);
            }

            // Colisión del outbox (sos:push:{alertId}:{userId} ya existe =
            // alertId repetido, imposible con id nuevo): re-superficie como
            // colisión de idempotencia para no enmascarar el 500.
            throw;
        }
    }

    public async Task UpdateAsync(SosAlert alert, CancellationToken ct = default)
    {
        dbContext.SosAlerts.Update(alert);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> AttendAsync(
        Guid alertId,
        Guid staffUserId,
        CancellationToken ct = default
    )
    {
        // Compare-and-set atómico en SQL: solo gana la transición si la alerta
        // SIGUE Activa en el momento del UPDATE (carrera attend/cancel →
        // exactamente una terminal, la otra ve 0 filas afectadas → 409).
        var affected = await dbContext
            .SosAlerts.Where(x => x.Id == alertId && x.Status == SosAlertStatus.Activa)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Status, SosAlertStatus.Atendida)
                        .SetProperty(x => x.AttendedBy, staffUserId)
                        .SetProperty(x => x.AttendedAt, DateTime.UtcNow),
                ct
            );
        return affected == 1;
    }

    public async Task<bool> CancelAsync(
        Guid alertId,
        Guid cancelledByUserId,
        CancellationToken ct = default
    )
    {
        var affected = await dbContext
            .SosAlerts.Where(x => x.Id == alertId && x.Status == SosAlertStatus.Activa)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Status, SosAlertStatus.Cancelada)
                        .SetProperty(x => x.CancelledBy, cancelledByUserId)
                        .SetProperty(x => x.CancelledAt, DateTime.UtcNow),
                ct
            );
        return affected == 1;
    }

    public async Task<(IReadOnlyList<SosAlertListItemDto> Items, int Total)> ListForStaffAsync(
        IReadOnlyCollection<Guid>? patientIds,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.SosAlerts.AsNoTracking().AsQueryable();

        // Scope (D5): null = bypass (roles de administración); lista vacía =
        // denegar (Contains sobre conjunto vacío → 0 filas, nunca se abre).
        if (patientIds is not null)
        {
            query = query.Where(x => patientIds.Contains(x.PatientId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status.ToString() == status);
        }

        var total = await query.CountAsync(ct);

        // Proyección directa a DTO en SQL: el nombre del paciente se resuelve
        // en el servidor; el contacto de emergencia y las coordenadas NUNCA
        // salen del listado (sin PII innecesaria ni siquiera en memoria).
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(x => new SosAlertListItemDto(
                x.Id,
                x.PatientId,
                x.Patient == null ? null : (x.Patient.FirstName + " " + x.Patient.LastName).Trim(),
                x.Status.ToString(),
                x.CreatedAt,
                x.AttendedBy,
                x.AttendedAt,
                x.CancelledBy,
                x.CancelledAt,
                x.SmsChannelStatus.ToString(),
                x.VoiceChannelStatus.ToString(),
                x.PushChannelStatus.ToString()
            ))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Guid>> ResolveScopedPatientIdsAsync(
        Guid? professionalId,
        Guid? activeClinicId,
        Guid? activeOrganizationId,
        CancellationToken ct = default
    )
    {
        var ids = new List<Guid>();

        // 1) Asignación directa activa (patient_professionals).
        if (professionalId is { } professional)
        {
            ids.AddRange(
                await dbContext
                    .PatientProfessionalAssignments.AsNoTracking()
                    .Where(pp => pp.ProfessionalId == professional && pp.Status == "Active")
                    .Select(pp => pp.PatientId)
                    .ToListAsync(ct)
            );
        }

        // 2) Pacientes de la clínica activa del contexto.
        if (activeClinicId is { } clinicId)
        {
            ids.AddRange(
                await dbContext
                    .PatientProfiles.AsNoTracking()
                    .Where(p => p.ClinicId == clinicId)
                    .Select(p => p.Id)
                    .ToListAsync(ct)
            );
        }

        // 3) Pacientes cuya clínica pertenece a la organización activa.
        if (activeOrganizationId is { } organizationId)
        {
            ids.AddRange(
                await (
                    from patient in dbContext.PatientProfiles.AsNoTracking()
                    join clinic in dbContext.Clinics.AsNoTracking()
                        on patient.ClinicId equals clinic.Id
                    where clinic.OrganizationId == organizationId
                    select patient.Id
                ).ToListAsync(ct)
            );
        }

        return ids.Distinct().ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetAssignedStaffUserIdsAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await (
            from assignment in dbContext.PatientProfessionalAssignments.AsNoTracking()
            join professional in dbContext.Professionals.AsNoTracking()
                on assignment.ProfessionalId equals professional.Id
            join employee in dbContext.Employees.AsNoTracking()
                on professional.EmployeeId equals employee.Id
            where
                assignment.PatientId == patientId
                && assignment.Status == "Active"
                && employee.UserId != null
            select employee.UserId!.Value
        )
            .Distinct()
            .ToListAsync(ct);

    public async Task<bool> IsStaffScopedToPatientAsync(
        Guid patientId,
        Guid? professionalId,
        Guid? activeClinicId,
        Guid? activeOrganizationId,
        CancellationToken ct = default
    )
    {
        // 1) Asignación directa activa (D5).
        if (
            professionalId is { } profId
            && await dbContext
                .PatientProfessionalAssignments.AsNoTracking()
                .AnyAsync(
                    x =>
                        x.PatientId == patientId
                        && x.ProfessionalId == profId
                        && x.Status == "Active",
                    ct
                )
        )
        {
            return true;
        }

        // 2) Ámbito de clínica u organización coincidente con el contexto activo.
        var patient = await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(x => x.Id == patientId)
            .Select(x => new { x.ClinicId, ClinicOrganizationId = x.Clinic!.OrganizationId })
            .FirstOrDefaultAsync(ct);

        if (patient is null)
        {
            return false;
        }

        return (activeClinicId.HasValue && patient.ClinicId == activeClinicId)
            || (
                activeOrganizationId.HasValue
                && patient.ClinicOrganizationId == activeOrganizationId
            );
    }

    private static bool IsUniqueViolation(DbUpdateException ex, out string constraintName)
    {
        constraintName = string.Empty;
        if (
            ex.InnerException
            is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        )
        {
            return false;
        }

        constraintName = pg.ConstraintName ?? string.Empty;
        return true;
    }
}
