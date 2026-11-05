using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class NutritionCaseStore(AppDbContext db) : INutritionCaseStore
{
    public async Task<NutritionCaseContext?> GetContextAsync(Guid patientId, CancellationToken ct)
    {
        var patient = await db.PatientProfiles.AsNoTracking().Where(x => x.Id == patientId && x.DeletedAt == null)
            .Select(x => new { x.DateOfBirth, x.Gender, x.Timezone }).SingleOrDefaultAsync(ct);
        if (patient is null) return null;
        var assignments = await db.HealthTestAssignments.AsNoTracking().Where(a => a.PatientId == patientId
                && a.Status != HealthTestAssignmentStatus.cancelled
                && (a.BatteryAssignment == null || a.BatteryAssignment.Status != HealthTestAssignmentStatus.cancelled))
            .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.BatteryAssignmentId, InstrumentId = a.Version!.InstrumentId, a.Status,
                Completed = a.Evaluations.Any(e => e.Status == HealthTestEvaluationStatus.completed) }).ToListAsync(ct);
        var latest = assignments.GroupBy(x => x.InstrumentId).Select(g => g.First()).ToArray();
        return new(patient.DateOfBirth is { } dob ? DateOnly.FromDateTime(dob) : null, patient.Gender, patient.Timezone,
            latest.Length >= 9 && latest.All(x => x.Status == HealthTestAssignmentStatus.completed && x.Completed),
            assignments.FirstOrDefault(x => x.BatteryAssignmentId != null)?.BatteryAssignmentId);
    }
    public async Task<bool> CanAccessAsync(Guid patientId, NutritionActor actor, CancellationToken ct)
        => await db.PatientProfiles.AsNoTracking().AnyAsync(p => p.Id == patientId && p.DeletedAt == null
            && (actor.ViewAll || actor.ProfessionalId != null && db.PatientProfessionalAssignments.Any(a =>
                a.PatientId == p.Id && a.ProfessionalId == actor.ProfessionalId && a.Status == "Active")), ct);

    public async Task<T> ExecuteAsync<T>(Guid patientId, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM app.patient_profiles WHERE id = {patientId} FOR UPDATE", ct);
            try
            {
                var result = await operation(ct); await tx.CommitAsync(ct); return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BusinessRuleViolationException("La prescripción cambió; vuelve a cargar la revisión.");
            }
        });
    }
    public async Task<int> NextVersionAsync(Guid cycleId, CancellationToken ct)
        => (await db.NutritionPrescriptions.Where(x => x.CycleId == cycleId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
    public async Task<IReadOnlyList<NutritionPrescription>> PublishedAsync(Guid patientId, CancellationToken ct)
        => await db.NutritionPrescriptions.AsNoTracking().Where(x => x.PatientId == patientId && x.IsPublished).ToListAsync(ct);
    public async Task AddEventAsync(NutritionPrescriptionEvent entry, CancellationToken ct)
    {
        db.NutritionPrescriptionEvents.Add(entry); await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<NutritionPrescriptionEvent>> HistoryAsync(Guid id, CancellationToken ct)
        => await db.NutritionPrescriptionEvents.AsNoTracking().Where(x => x.PrescriptionId == id)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
}
