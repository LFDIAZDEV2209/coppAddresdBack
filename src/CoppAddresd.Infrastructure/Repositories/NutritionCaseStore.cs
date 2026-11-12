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
        var patient = await db
            .PatientProfiles.AsNoTracking()
            .Where(x => x.Id == patientId && x.DeletedAt == null)
            .Select(x => new
            {
                x.DateOfBirth,
                x.Gender,
                x.Timezone,
            })
            .SingleOrDefaultAsync(ct);
        if (patient is null)
            return null;
        var initial = await db.HealthTestBatteryAssignments.AsNoTracking()
            .Where(b=>b.PatientId==patientId && b.Battery!.Code=="bateria-inicial" && b.Battery.IsActive && b.Battery.AutoAssignOnPatientCreate
                && b.Status!=HealthTestAssignmentStatus.cancelled && b.Status!=HealthTestAssignmentStatus.expired)
            .OrderByDescending(b=>b.AssignedAt).ThenByDescending(b=>b.Id)
            .Select(b=>new {b.Id,b.BatteryId}).FirstOrDefaultAsync(ct);
        var assignments = await db.HealthTestAssignments.AsNoTracking()
            .Where(a => a.PatientId == patientId && (initial == null || a.BatteryAssignmentId == initial.Id)
                && a.Status != HealthTestAssignmentStatus.cancelled && a.Status != HealthTestAssignmentStatus.expired
                && (a.BatteryAssignment == null || a.BatteryAssignment.Status != HealthTestAssignmentStatus.cancelled))
            .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.BatteryAssignmentId, a.VersionId, InstrumentId = a.Version!.InstrumentId, a.Status, a.AssignedAt,
                Completed = a.Evaluations.Any(e => e.Status == HealthTestEvaluationStatus.completed
                    && e.PatientId == patientId && e.VersionId == a.VersionId),
            }).ToListAsync(ct);
        var required = initial is null ? [] : await db.HealthTestBatteryItems.AsNoTracking()
            .Where(i => i.BatteryId == initial.BatteryId && i.IsRequired)
            .Select(i => new { i.InstrumentId, i.VersionId }).ToArrayAsync(ct);
        var latest = assignments.GroupBy(x => x.InstrumentId).Select(g => g.First()).ToArray();
        var testsComplete = initial is not null
            ? required.Length > 0 && required.All(item => assignments.Any(x => x.InstrumentId == item.InstrumentId
                && (item.VersionId == null || item.VersionId == x.VersionId)
                && x.Status == HealthTestAssignmentStatus.completed && x.Completed))
            : latest.Length >= 9 && latest.All(x => x.Status == HealthTestAssignmentStatus.completed && x.Completed);
        // El perfil medido proviene de la batería completa más reciente: un re-test
        // puntual posterior no debe ensombrecer el baseline de 9 instrumentos.
        var batteryPointer = assignments
            .Where(x => x.BatteryAssignmentId != null)
            .GroupBy(x => x.BatteryAssignmentId)
            .Select(g => new
            {
                BatteryId = g.Key!.Value,
                AssignedAt = g.Max(x => x.AssignedAt),
                Total = g.Count(),
                Completed = g.Count(x =>
                    x.Status == HealthTestAssignmentStatus.completed && x.Completed
                ),
            })
            .ToList();
        var complete = initial?.Id ?? (batteryPointer
            .Where(x => x.Total >= 9 && x.Completed >= x.Total)
            .OrderByDescending(x => x.AssignedAt)
            .FirstOrDefault()
            is { } full
            ? full.BatteryId
            : batteryPointer.OrderByDescending(x => x.AssignedAt).FirstOrDefault()?.BatteryId);
        return new(
            patient.DateOfBirth is { } dob ? DateOnly.FromDateTime(dob) : null,
            patient.Gender,
            patient.Timezone,
            testsComplete,
            complete
        );
    }

    public async Task<bool> CanAccessAsync(
        Guid patientId,
        NutritionActor actor,
        CancellationToken ct
    ) =>
        await db
            .PatientProfiles.AsNoTracking()
            .AnyAsync(
                p =>
                    p.Id == patientId
                    && p.DeletedAt == null
                    && (
                        actor.ViewAll
                        || actor.ProfessionalId != null
                            && db.PatientProfessionalAssignments.Any(a =>
                                a.PatientId == p.Id
                                && a.ProfessionalId == actor.ProfessionalId
                                && a.Status == "Active"
                            )
                    ),
                ct
            );

    public async Task<T> ExecuteAsync<T>(
        Guid patientId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct
    )
    {
        return await db
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(
                async strategyCt =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = await db.Database.BeginTransactionAsync(strategyCt);
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT id FROM app.patient_profiles WHERE id = {patientId} FOR UPDATE",
                        strategyCt
                    );
                    try
                    {
                        var result = await operation(strategyCt);
                        await tx.CommitAsync(strategyCt);
                        return result;
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        throw new BusinessRuleViolationException(
                            "La prescripción cambió; vuelve a cargar la revisión."
                        );
                    }
                },
                ct
            );
    }

    public async Task<int> NextVersionAsync(Guid cycleId, CancellationToken ct) =>
        (
            await db
                .NutritionPrescriptions.Where(x => x.CycleId == cycleId)
                .MaxAsync(x => (int?)x.Version, ct)
            ?? 0
        ) + 1;

    public async Task<IReadOnlyList<NutritionPrescription>> PublishedAsync(
        Guid patientId,
        CancellationToken ct
    ) =>
        await db
            .NutritionPrescriptions.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.IsPublished)
            .ToListAsync(ct);

    public async Task AddEventAsync(NutritionPrescriptionEvent entry, CancellationToken ct)
    {
        db.NutritionPrescriptionEvents.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NutritionPrescriptionEvent>> HistoryAsync(
        Guid id,
        CancellationToken ct
    ) =>
        await db
            .NutritionPrescriptionEvents.AsNoTracking()
            .Where(x => x.PrescriptionId == id)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
}
