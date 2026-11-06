using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Proyecciones sin cargar menús para identidad, proceso y página de historial.</summary>
public sealed class NutritionWorkspaceRepository(AppDbContext db) : INutritionWorkspaceRepository
{
    public Task<NutritionWorkspaceIdentity?> IdentityAsync(Guid patientId, CancellationToken ct)
        => db.PatientProfiles.AsNoTracking().Where(x => x.Id == patientId && x.DeletedAt == null)
            .Select(x => new NutritionWorkspaceIdentity(x.Id, x.FirstName + " " + x.LastName, x.DocumentNumber, x.Timezone))
            .SingleOrDefaultAsync(ct);
    public Task<Guid?> LatestPrescriptionIdAsync(Guid patientId, CancellationToken ct)
        => db.NutritionPrescriptions.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    public Task<NutritionDraftReservation?> GenerationAsync(Guid patientId, CancellationToken ct)
        => db.NutritionDraftReservations.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task<NutritionHistoryPage> HistoryAsync(Guid patientId, int page, int pageSize, CancellationToken ct)
    {
        var query = from e in db.NutritionPrescriptionEvents.AsNoTracking()
                    join p in db.NutritionPrescriptions.AsNoTracking() on e.PrescriptionId equals p.Id
                    where p.PatientId == patientId
                    select new { e, p.CycleId, p.Version };
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.e.OccurredAt).ThenByDescending(x => x.e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NutritionHistoryItem(x.e.Id, x.e.PrescriptionId, x.CycleId, x.Version, x.e.ActorUserId,
                x.e.ActorUserId == CoppAddresd.Application.Features.Nutrition.Cases.NutritionSystemActor.Id ? "system:nutrition" : "Profesional",
                x.e.OccurredAt, x.e.Action, x.e.Revision, x.e.Comment, x.e.CriticalCount, x.e.CriticalConfirmed, x.e.AutoApproved))
            .ToListAsync(ct);
        return new(items, total, page, pageSize);
    }
}
