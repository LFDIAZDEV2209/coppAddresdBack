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
}