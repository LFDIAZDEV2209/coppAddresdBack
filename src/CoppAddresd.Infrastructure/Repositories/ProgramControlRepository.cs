using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del control conversacional del programa: candidatos activos
/// (inscripción + perfil con usuario) y filas de <c>program_controls</c>.
/// Estilo espejo de DeviceTokenRepository: operaciones acotadas, SaveChanges
/// por operación.
/// </summary>
public sealed class ProgramControlRepository(AppDbContext dbContext) : IProgramControlRepository
{
    public async Task<IReadOnlyList<ProgramControlEnrollmentCandidate>> ListActiveCandidatesAsync(
        DateOnly startLocalDateCutoff, CancellationToken ct = default)
        => await dbContext.ProgramEnrollments
            .AsNoTracking()
            .Include(e => e.Patient)
            .Where(e => e.Status == ProgramEnrollmentStatus.Active
                && e.StartLocalDate >= startLocalDateCutoff
                && e.Patient != null
                && e.Patient.UserId != null)
            .Select(e => new ProgramControlEnrollmentCandidate(
                e.Id, e.PatientId, e.Patient!.UserId!.Value, e.Timezone, e.StartLocalDate))
            .ToListAsync(ct);

    public async Task<ProgramControl?> GetAsync(
        Guid enrollmentId, int milestoneDay, CancellationToken ct = default)
        => await dbContext.ProgramControls
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.EnrollmentId == enrollmentId && c.MilestoneDay == milestoneDay, ct);

    public async Task AddAsync(ProgramControl control, CancellationToken ct = default)
    {
        dbContext.ProgramControls.Add(control);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProgramControl control, CancellationToken ct = default)
    {
        dbContext.ProgramControls.Update(control);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ProgramControlEnrollmentCandidate?> GetCandidateAsync(
        Guid enrollmentId, CancellationToken ct = default)
        => await dbContext.ProgramEnrollments
            .AsNoTracking()
            .Where(e => e.Id == enrollmentId
                && e.Patient != null
                && e.Patient.UserId != null)
            .Select(e => new ProgramControlEnrollmentCandidate(
                e.Id, e.PatientId, e.Patient!.UserId!.Value, e.Timezone, e.StartLocalDate))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProgramControl>> ListAsync(
        Guid? enrollmentId = null,
        Guid? patientId = null,
        ProgramControlStatus? status = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        // La navegación Enrollment se carga (Include) únicamente para exponer
        // el PatientId en el mapeo del controlador sin N+1; el filtro por
        // paciente traduce el mismo join.
        IQueryable<ProgramControl> query = dbContext.ProgramControls
            .AsNoTracking()
            .Include(c => c.Enrollment);

        if (enrollmentId is { } eid)
        {
            query = query.Where(c => c.EnrollmentId == eid);
        }

        if (patientId is { } pid)
        {
            query = query.Where(c => c.Enrollment!.PatientId == pid);
        }

        if (status is { } controlStatus)
        {
            query = query.Where(c => c.Status == controlStatus);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(limit ?? 100, 1, 500))
            .ToListAsync(ct);
    }

    public async Task<int> DeleteAsync(
        Guid? enrollmentId = null, CancellationToken ct = default)
    {
        // Se elimina por el change tracker (no ExecuteDelete) para mantener la
        // consistencia con el interceptor de auditoría: la migración adjunta un
        // trigger de auditoría a program_controls, y el actor se propaga por
        // GUC en SaveChanges (misma convención que DeviceTokenRepository).
        var query = dbContext.ProgramControls.AsQueryable();
        if (enrollmentId is { } eid)
        {
            query = query.Where(c => c.EnrollmentId == eid);
        }

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0)
        {
            return 0;
        }

        dbContext.ProgramControls.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<ProgramControl?> FindOpenControlForUserAsync(
        Guid authUserId, string? threadId = null, CancellationToken ct = default)
    {
        // Mapeo auth-user → paciente IDÉNTICO al de UploadLabExamCommandHandler
        // (GetByUserIdAsync): patient_profiles.user_id == authUserId, sin soft
        // delete. El join por navegaciones traduce el mismo predicado en una
        // sola query — no se inventa una segunda vía de resolución.
        IQueryable<ProgramControl> query = dbContext.ProgramControls
            .AsNoTracking()
            .Where(c => c.Enrollment != null
                && c.Enrollment.Patient != null
                && c.Enrollment.Patient.UserId == authUserId
                && c.Enrollment.Patient.DeletedAt == null
                && (c.Status == ProgramControlStatus.Sent
                    || c.Status == ProgramControlStatus.Responded
                    || c.Status == ProgramControlStatus.FollowedUp));

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            // Filtro de seguridad: el mensaje entrante debe pertenecer al
            // thread del control (un thread ajeno nunca abre otro control).
            query = query.Where(c => c.ThreadId == threadId);
        }

        return await query
            .OrderByDescending(c => c.SentAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> MarkRespondedAsync(
        Guid id, DateTime respondedAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id
                && (c.Status == ProgramControlStatus.Sent
                    || c.Status == ProgramControlStatus.FollowedUp))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.Responded)
                    .SetProperty(c => c.RespondedAt, respondedAt)
                    .SetProperty(c => c.UpdatedAt, respondedAt),
                ct);

        return updated > 0;
    }

    public async Task<bool> MarkCompletedAsync(
        Guid id, Guid examBatchId, DateTime completedAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id
                && (c.Status == ProgramControlStatus.Sent
                    || c.Status == ProgramControlStatus.Responded
                    || c.Status == ProgramControlStatus.FollowedUp))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.Completed)
                    .SetProperty(c => c.CompletedAt, completedAt)
                    .SetProperty(c => c.ExamBatchId, examBatchId)
                    .SetProperty(c => c.UpdatedAt, completedAt),
                ct);

        return updated > 0;
    }

    public async Task<bool> MarkClosedDeclinedAsync(
        Guid id, DateTime closedAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id
                && (c.Status == ProgramControlStatus.Sent
                    || c.Status == ProgramControlStatus.Responded
                    || c.Status == ProgramControlStatus.FollowedUp))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.ClosedWithoutExam)
                    .SetProperty(c => c.ClosedReason, "declined")
                    .SetProperty(c => c.UpdatedAt, closedAt),
                ct);

        return updated > 0;
    }

    public async Task<bool> MarkFollowedUpAsync(
        Guid id, DateTime followupSentAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id && c.Status == ProgramControlStatus.Sent)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.FollowedUp)
                    .SetProperty(c => c.FollowupSentAt, followupSentAt)
                    .SetProperty(c => c.UpdatedAt, followupSentAt),
                ct);

        return updated > 0;
    }

    public async Task<bool> MarkMissedAsync(
        Guid id, DateTime missedAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id && c.Status == ProgramControlStatus.FollowedUp)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.Missed)
                    .SetProperty(c => c.UpdatedAt, missedAt),
                ct);

        return updated > 0;
    }

    public async Task<bool> MarkNoUploadTimeoutAsync(
        Guid id, DateTime closedAt, CancellationToken ct = default)
    {
        var updated = await dbContext.ProgramControls
            .Where(c => c.Id == id && c.Status == ProgramControlStatus.Responded)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.Status, ProgramControlStatus.ClosedWithoutExam)
                    .SetProperty(c => c.ClosedReason, "no_upload_timeout")
                    .SetProperty(c => c.UpdatedAt, closedAt),
                ct);

        return updated > 0;
    }

    public async Task<IReadOnlyList<ProgramControlDueItem>> ListDueForFollowupAsync(
        DateTime utcNow, int followupHours, int limit = 100, CancellationToken ct = default)
    {
        var cutoff = utcNow.AddHours(-followupHours);

        // El join al paciente (con usuario auth) es obligatorio: el follow-up
        // se envía por el MISMO canal que el envío de apertura (push + chat) y
        // sin usuario no hay push ni inyección proactiva.
        return await dbContext.ProgramControls
            .AsNoTracking()
            .Where(c => c.Status == ProgramControlStatus.Sent
                && c.FollowupSentAt == null
                && c.SentAt != null
                && c.SentAt <= cutoff
                && c.Enrollment != null
                && c.Enrollment.Patient != null
                && c.Enrollment.Patient.UserId != null)
            .OrderBy(c => c.SentAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(c => new ProgramControlDueItem(c, c.Enrollment!.Timezone, c.Enrollment!.Patient!.UserId!.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProgramControlDueItem>> ListDueForMissAsync(
        DateTime utcNow, int missAfterFollowupHours, int limit = 100, CancellationToken ct = default)
    {
        var cutoff = utcNow.AddHours(-missAfterFollowupHours);

        return await dbContext.ProgramControls
            .AsNoTracking()
            .Where(c => c.Status == ProgramControlStatus.FollowedUp
                && c.FollowupSentAt != null
                && c.FollowupSentAt <= cutoff
                && c.Enrollment != null
                && c.Enrollment.Patient != null
                && c.Enrollment.Patient.UserId != null)
            .OrderBy(c => c.FollowupSentAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(c => new ProgramControlDueItem(c, c.Enrollment!.Timezone, c.Enrollment!.Patient!.UserId!.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProgramControlDueItem>> ListTimedOutNoUploadAsync(
        DateTime utcNow, int noUploadCloseHours, int limit = 100, CancellationToken ct = default)
    {
        var cutoff = utcNow.AddHours(-noUploadCloseHours);

        return await dbContext.ProgramControls
            .AsNoTracking()
            .Where(c => c.Status == ProgramControlStatus.Responded
                && c.RespondedAt != null
                && c.RespondedAt <= cutoff
                && c.Enrollment != null
                && c.Enrollment.Patient != null
                && c.Enrollment.Patient.UserId != null)
            .OrderBy(c => c.RespondedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(c => new ProgramControlDueItem(c, c.Enrollment!.Timezone, c.Enrollment!.Patient!.UserId!.Value))
            .ToListAsync(ct);
    }
}