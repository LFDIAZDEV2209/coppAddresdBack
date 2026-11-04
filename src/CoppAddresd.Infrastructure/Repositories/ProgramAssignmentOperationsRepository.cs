using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Lecturas ERP acotadas y claims del job: el filtro de paciente siempre precede a la paginación.</summary>
public sealed class ProgramAssignmentOperationsRepository(AppDbContext db) : IProgramAssignmentOperationsRepository
{
    private const string QueueWhere = """
        FROM app.program_assignment_recommendations r
        JOIN app.patient_profiles p ON p.id = r.patient_id
        WHERE p.deleted_at IS NULL
          AND (@scope IS NULL OR r.patient_id = ANY(@scope))
          AND ((@status IS NULL AND r.state IN ('proposed','pending-narrative')) OR @status='all' OR r.state=@status)
          AND (@search IS NULL OR (p.first_name || ' ' || p.last_name) ILIKE @search OR p.document_number ILIKE @search)
        """;

    public async Task<AssignmentQueuePage> ListAsync(string? status, int page, int pageSize, string? search,
        IReadOnlyList<Guid>? scopedPatientIds, DateTime utcNow, CancellationToken ct)
    {
        NpgsqlParameter[] Parameters() => [
            new("scope", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = (object?)scopedPatientIds?.ToArray() ?? DBNull.Value },
            new("status", NpgsqlDbType.Text) { Value = (object?)status ?? DBNull.Value },
            new("search", NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(search) ? DBNull.Value : "%" + search.Trim() + "%" }];
        var total = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" " + QueueWhere, Parameters()).SingleAsync(ct);
        var sql = """
            SELECT r.id AS "Id", r.enrollment_id AS "EnrollmentId", r.patient_id AS "PatientId",
              (p.first_name || ' ' || p.last_name) AS "PatientName", p.document_number AS "DocumentNumber",
              r.state AS "State", r.created_at AS "CreatedAt",
              greatest(0, floor(extract(epoch FROM (@now-r.created_at))/86400))::int AS "WaitingDays",
              COALESCE((SELECT max(CASE lower(v #>> ARRAY[]::text[])
                WHEN 'critical' THEN 4 WHEN 'severe' THEN 4 WHEN 'critico' THEN 4 WHEN 'critico/a' THEN 4
                WHEN 'high' THEN 3 WHEN 'alto' THEN 3 WHEN 'stage-2' THEN 3 WHEN 'moderately-severe' THEN 3
                WHEN 'moderate' THEN 2 WHEN 'moderada' THEN 2 WHEN 'stage-1' THEN 2
                WHEN 'low' THEN 1 WHEN 'bajo' THEN 1 WHEN 'mild' THEN 1 ELSE 0 END)
                FROM jsonb_path_query(r.profile_snapshot, '$.**.severity') v),0)::int AS "MaxSeverity",
              (r.narrative_es IS NULL OR r.narrative_en IS NULL) AS "NarrativePending"
            """ + QueueWhere + " ORDER BY \"WaitingDays\" DESC, \"MaxSeverity\" DESC, \"PatientName\" ASC, r.patient_id, r.id LIMIT @limit OFFSET @offset";
        var args = Parameters().Concat(new NpgsqlParameter[] { new("now", utcNow), new("limit", pageSize), new("offset", ((long)page-1)*pageSize) }).ToArray();
        var rows = await db.Database.SqlQueryRaw<QueueRow>(sql, args).ToListAsync(ct);
        return new(rows.Select(r => new AssignmentQueueItem(r.Id, r.EnrollmentId, r.PatientId, r.PatientName,
            r.DocumentNumber, r.State, r.CreatedAt, r.WaitingDays, r.MaxSeverity, r.NarrativePending)).ToArray(),
            total, page, pageSize, Math.Max(1, (int)Math.Ceiling(total/(double)pageSize)));
    }

    public async Task<(AssignmentDetailDto? Detail, bool Changed)> DismissAsync(Guid id, Guid actorId,
        IReadOnlyList<Guid>? scopedPatientIds, DateTime utcNow, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                // UPDATE condicional serializa dismiss/apply por la fila de recomendación.
                // El predicado de alcance forma parte de la escritura, no solo de la lectura posterior.
                var changed = await db.ProgramAssignmentRecommendations
                    .Where(r => r.Id == id && (scopedPatientIds == null || scopedPatientIds.Contains(r.PatientId))
                        && db.PatientProfiles.Any(p => p.Id == r.PatientId && p.DeletedAt == null)
                        && (r.State == "proposed" || r.State == "pending-narrative"))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "dismissed")
                        .SetProperty(r => r.DismissedBy, actorId).SetProperty(r => r.DismissedAt, utcNow)
                        .SetProperty(r => r.UpdatedAt, utcNow), ct);
                var detail = await GetDetailAsync(id, scopedPatientIds, ct);
                if (detail is null) return ((AssignmentDetailDto?)null, false);
                if (changed == 0 && detail.State != "dismissed")
                    throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException(
                        "ASSIGNMENT_NOT_DISMISSIBLE: la recomendación ya no está pendiente.");
                if (changed > 0)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO audit.activity_logs
                            (occurred_at, action, schema_name, table_name, record_id, actor_type, user_id)
                        VALUES ({utcNow}, 'ProgramAssignmentDismissed', 'app',
                            'program_assignment_recommendations', {id.ToString()}, 'USER', {actorId})
                        """, ct);
                await transaction.CommitAsync(ct);
                return (detail, changed > 0);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<AssignmentDetailDto?> GetDetailAsync(Guid id, IReadOnlyList<Guid>? scopedPatientIds, CancellationToken ct)
    {
        var row = await (from r in db.ProgramAssignmentRecommendations.AsNoTracking()
            join p in db.PatientProfiles.AsNoTracking() on r.PatientId equals p.Id
            join e in db.ProgramEnrollments.AsNoTracking() on r.EnrollmentId equals e.Id
            where r.Id == id && p.DeletedAt == null && (scopedPatientIds == null || scopedPatientIds.Contains(r.PatientId))
            select new { Recommendation = r, PatientName = p.FirstName + " " + p.LastName, p.DocumentNumber, Enrollment = e }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var rec = row.Recommendation; var enrollment = row.Enrollment;
        return new(rec.Id, rec.PatientId, row.PatientName, row.DocumentNumber,
            new(enrollment.Id, enrollment.Status.ToString(), enrollment.CurrentWeekNumber, enrollment.StartLocalDate, enrollment.StartedAt, enrollment.Timezone),
            JsonSerializer.Deserialize<JsonElement>(rec.ProfileSnapshot), JsonSerializer.Deserialize<JsonElement>(rec.Proposal),
            rec.NarrativeEs, rec.NarrativeEn, string.IsNullOrWhiteSpace(rec.NarrativeEs) || string.IsNullOrWhiteSpace(rec.NarrativeEn),
            rec.State, rec.EngineVersion, rec.ModelName, rec.AppliedBy, rec.DismissedBy, rec.CreatedAt, rec.UpdatedAt, rec.AppliedAt, rec.DismissedAt);
    }

    public async Task<IReadOnlyList<AssignmentRecoveryCandidate>> ListRecoveryAsync(DateTime utcNow, int maxAttempts, int limit, CancellationToken ct)
    {
        // Misma división relacional del submit, ampliada por lote y con backoff persistido.
        var sql = ProgramAssignmentRepository.CompletedBatterySql
            .Replace("SELECT ba.id AS \"Value\"", "SELECT ba.id AS \"BatteryAssignmentId\", ba.patient_id AS \"PatientId\"")
            .Replace("ba.patient_id = @patientId AND ba.id = @batteryAssignmentId", "true") + """
            AND NOT EXISTS (SELECT 1 FROM app.program_enrollments e WHERE e.patient_id=ba.patient_id
              AND (e.status='Active' OR (e.status='PendingAssignment' AND EXISTS
                (SELECT 1 FROM app.program_assignment_recommendations r WHERE r.enrollment_id=e.id))))
            AND NOT EXISTS (SELECT 1 FROM app.program_assignment_recoveries x WHERE x.battery_assignment_id=ba.id
              AND (x.attempts>=@maxAttempts OR x.next_attempt_at>@now))
            ORDER BY ba.assigned_at, ba.id LIMIT @limit
            """;
        var rows = await db.Database.SqlQueryRaw<RecoveryRow>(sql, new NpgsqlParameter("now", utcNow),
            new NpgsqlParameter("maxAttempts", Math.Clamp(maxAttempts,1,20)), new NpgsqlParameter("limit", Math.Clamp(limit,1,500))).ToListAsync(ct);
        return rows.Select(x => new AssignmentRecoveryCandidate(x.PatientId,x.BatteryAssignmentId)).ToArray();
    }

    public async Task<bool> TryClaimRecoveryAsync(Guid batteryAssignmentId, DateTime utcNow, int maxAttempts, int baseDelaySeconds, CancellationToken ct)
    {
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.program_assignment_recoveries (battery_assignment_id,attempts,next_attempt_at,updated_at)
            VALUES ({batteryAssignmentId},1,{utcNow}+make_interval(secs=>{Math.Max(1,baseDelaySeconds)}),{utcNow})
            ON CONFLICT (battery_assignment_id) DO UPDATE SET attempts=program_assignment_recoveries.attempts+1,
              next_attempt_at={utcNow}+make_interval(secs=>{Math.Max(1,baseDelaySeconds)}*power(2,LEAST(program_assignment_recoveries.attempts,10))),
              updated_at={utcNow}
            WHERE program_assignment_recoveries.attempts < {Math.Clamp(maxAttempts,1,20)}
              AND program_assignment_recoveries.next_attempt_at <= {utcNow}
            """, ct);
        return count > 0;
    }
    public Task<bool> HasRecommendationForPatientAsync(Guid patientId, CancellationToken ct) =>
        db.ProgramAssignmentRecommendations.AsNoTracking().AnyAsync(r => r.PatientId==patientId
            && r.Enrollment!.Status==ProgramEnrollmentStatus.PendingAssignment, ct);
    public async Task<IReadOnlyList<AssignmentFallbackCandidate>> ListFallbacksAsync(DateTime cutoff, int limit, CancellationToken ct) =>
        await db.ProgramAssignmentRecommendations.AsNoTracking()
            .Where(r => (r.State=="proposed" || r.State=="pending-narrative") && r.CreatedAt<=cutoff)
            .OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id).Take(Math.Clamp(limit,1,500))
            .Select(r=>new AssignmentFallbackCandidate(r.Id,r.CreatedAt)).ToListAsync(ct);

    private sealed class QueueRow
    {
        public Guid Id { get; set; } public Guid EnrollmentId { get; set; } public Guid PatientId { get; set; }
        public string PatientName { get; set; } = ""; public string? DocumentNumber { get; set; }
        public string State { get; set; } = ""; public DateTime CreatedAt { get; set; }
        public int WaitingDays { get; set; } public int MaxSeverity { get; set; } public bool NarrativePending { get; set; }
    }
    private sealed class RecoveryRow { public Guid PatientId { get; set; } public Guid BatteryAssignmentId { get; set; } }
}
