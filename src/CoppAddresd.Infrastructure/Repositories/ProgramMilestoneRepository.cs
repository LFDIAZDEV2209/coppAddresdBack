using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del envío de recordatorios de hito del programa: candidatos
/// activos (inscripción + perfil con usuario) y filas de
/// <c>program_milestone_sends</c>. Estilo espejo de DeviceTokenRepository:
/// operaciones acotadas, SaveChanges por operación.
/// </summary>
public sealed class ProgramMilestoneRepository(AppDbContext dbContext) : IProgramMilestoneRepository
{
    public async Task<IReadOnlyList<MilestoneEnrollmentCandidate>> ListActiveCandidatesAsync(
        DateOnly startLocalDateCutoff, CancellationToken ct = default)
        => await dbContext.ProgramEnrollments
            .AsNoTracking()
            .Include(e => e.Patient)
            .Where(e => e.Status == ProgramEnrollmentStatus.Active
                && e.StartLocalDate >= startLocalDateCutoff
                && e.Patient != null
                && e.Patient.UserId != null)
            .Select(e => new MilestoneEnrollmentCandidate(
                e.Id, e.PatientId, e.Patient!.UserId!.Value, e.Timezone, e.StartLocalDate))
            .ToListAsync(ct);

    public async Task<ProgramMilestoneSend?> GetSendAsync(
        Guid enrollmentId, int milestoneDay, CancellationToken ct = default)
        => await dbContext.ProgramMilestoneSends
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.EnrollmentId == enrollmentId && s.MilestoneDay == milestoneDay, ct);

    public async Task AddAsync(ProgramMilestoneSend send, CancellationToken ct = default)
    {
        dbContext.ProgramMilestoneSends.Add(send);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProgramMilestoneSend send, CancellationToken ct = default)
    {
        dbContext.ProgramMilestoneSends.Update(send);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<MilestoneEnrollmentCandidate?> GetCandidateAsync(
        Guid enrollmentId, CancellationToken ct = default)
        => await dbContext.ProgramEnrollments
            .AsNoTracking()
            .Where(e => e.Id == enrollmentId
                && e.Patient != null
                && e.Patient.UserId != null)
            .Select(e => new MilestoneEnrollmentCandidate(
                e.Id, e.PatientId, e.Patient!.UserId!.Value, e.Timezone, e.StartLocalDate))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProgramMilestoneSend>> ListSendsAsync(
        Guid? enrollmentId = null,
        Guid? patientId = null,
        ProgramMilestoneSendStatus? status = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        // La navegación Enrollment se carga (Include) únicamente para exponer
        // el PatientId en el mapeo del controlador sin N+1; el filtro por
        // paciente traduce el mismo join.
        IQueryable<ProgramMilestoneSend> query = dbContext.ProgramMilestoneSends
            .AsNoTracking()
            .Include(s => s.Enrollment);

        if (enrollmentId is { } eid)
        {
            query = query.Where(s => s.EnrollmentId == eid);
        }

        if (patientId is { } pid)
        {
            query = query.Where(s => s.Enrollment!.PatientId == pid);
        }

        if (status is { } sendStatus)
        {
            query = query.Where(s => s.Status == sendStatus);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(Math.Clamp(limit ?? 100, 1, 500))
            .ToListAsync(ct);
    }

    public async Task<int> DeleteSendsAsync(
        Guid? enrollmentId = null, CancellationToken ct = default)
    {
        // Se elimina por el change tracker (no ExecuteDelete) para mantener la
        // consistencia con el interceptor de auditoría: la migración adjunta un
        // trigger de auditoría a program_milestone_sends, y el actor se propaga
        // por GUC en SaveChanges (misma convención que DeviceTokenRepository).
        var query = dbContext.ProgramMilestoneSends.AsQueryable();
        if (enrollmentId is { } eid)
        {
            query = query.Where(s => s.EnrollmentId == eid);
        }

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0)
        {
            return 0;
        }

        dbContext.ProgramMilestoneSends.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
        return rows.Count;
    }
}
