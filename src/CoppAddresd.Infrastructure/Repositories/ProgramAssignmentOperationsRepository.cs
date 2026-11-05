using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Lectura mínima de narrativa y claims del job; no expone cola ni ficha ERP.</summary>
public sealed class ProgramAssignmentOperationsRepository(AppDbContext db) : IProgramAssignmentOperationsRepository
{
    public Task<string?> GetNarrativeStateAsync(Guid id, IReadOnlyList<Guid>? scopedPatientIds, CancellationToken ct)
        => db.ProgramAssignmentRecommendations.AsNoTracking()
            .Where(r => r.Id == id && (scopedPatientIds == null || scopedPatientIds.Contains(r.PatientId))
                && db.PatientProfiles.Any(p => p.Id == r.PatientId && p.DeletedAt == null))
            .Select(r => r.State).SingleOrDefaultAsync(ct);

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

    private sealed class RecoveryRow { public Guid PatientId { get; set; } public Guid BatteryAssignmentId { get; set; } }
}
