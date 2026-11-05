using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private static NutritionPrescription Prescription(Guid patientId) => new()
    {
        PatientId = patientId, CreatedBy = Guid.NewGuid(), Provisional = true, CalorieTarget = 1350,
        ProteinTarget = 80.2, CarbsTarget = 150.5, FatTarget = 45.3, FiberTarget = 30, WaterMl = 2250,
        Instructions = "Revisar porciones", InstructionsEn = "Review portions",
        Meals = [new() { DayNumber = 1, MealType = "des", ScheduledTime = new(7, 0),
            Items = [new() { FoodItemId = "avena", Grams = 42.5, SortOrder = 1, Notes = "En seco" }] }]
    };

    [NutritionPostgresFact]
    public async Task NewAggregate_RoundTrips_ReplacesChildren_EnforcesRevisionAndCancellation()
    {
        Guid id;
        await using (var db = Context())
        {
            var patient = await PatientAsync(db); var prescription = Prescription(patient.Id); id = prescription.Id;
            await new NutritionPrescriptionRepository(db).AddAsync(prescription, CancellationToken.None);
        }
        NutritionPrescription stale;
        await using (var db = Context())
        {
            var repository = new NutritionPrescriptionRepository(db);
            stale = (await repository.GetByIdAsync(id, CancellationToken.None))!;
            Assert.Equal(42.5, Assert.Single(Assert.Single(stale.Meals).Items).Grams);
            Assert.Equal("Avena en hojuelas (seca)", stale.Meals.Single().Items.Single().FoodItem!.Name);
            Assert.Equal(1350, stale.CalorieTarget); Assert.True(stale.Provisional);
        }
        await using (var db = Context())
        {
            var repository = new NutritionPrescriptionRepository(db); var edit = (await repository.GetByIdAsync(id, CancellationToken.None))!;
            edit.Meals.Clear(); edit.Meals.Add(new() { DayNumber = 30, MealType = "cen", ScheduledTime = new(19, 0),
                Items = [new() { FoodItemId = "pollo", Grams = 120 }] });
            await repository.UpdateAsync(edit, CancellationToken.None); Assert.Equal(2, edit.Revision);
        }
        await using (var db = Context())
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new NutritionPrescriptionRepository(db).UpdateAsync(stale, CancellationToken.None));
        await using (var db = Context())
        {
            var repository = new NutritionPrescriptionRepository(db); var saved = (await repository.GetByIdAsync(id, CancellationToken.None))!;
            Assert.Equal(30, Assert.Single(saved.Meals).DayNumber);
            Assert.Equal("pollo", Assert.Single(saved.Meals.Single().Items).FoodItemId);
            Assert.Equal(1, await db.NutritionPrescriptionItems.CountAsync());
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetByIdAsync(id, cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(Prescription(saved.PatientId), cancel.Token));
        }
        await using var verify = Context(); Assert.Equal(1, await verify.NutritionPrescriptions.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task InvalidItem_RollsBackEntireNewAggregate_AndCatalogForeignKeyIsRequired()
    {
        await using var db = Context(); var patient = await PatientAsync(db);
        var plan = Prescription(patient.Id); plan.Meals.Single().Items.Single().FoodItemId = "no-existe";
        await Assert.ThrowsAsync<DbUpdateException>(() => new NutritionPrescriptionRepository(db).AddAsync(plan, CancellationToken.None));
        await using var verify = Context();
        Assert.Empty(await verify.NutritionPrescriptions.ToListAsync());
        Assert.Empty(await verify.NutritionPrescriptionMeals.ToListAsync());
        Assert.Empty(await verify.NutritionPrescriptionItems.ToListAsync());
    }

    [NutritionPostgresFact]
    public async Task ApprovedHistory_RemainsApproved_OnlyOneVersionIsPublishedPerCycle()
    {
        Guid patientId, cycleId;
        await using (var db = Context())
        {
            patientId = (await PatientAsync(db)).Id;
            var prior = Prescription(patientId); cycleId = prior.CycleId;
            prior.State = "aprobado"; prior.StartDate = new(2026, 11, 1); prior.EndDate = new(2026, 11, 30);
            prior.ApprovedBy = Guid.NewGuid(); prior.ApprovedAt = DateTime.UtcNow;
            var current = Prescription(patientId); current.CycleId = cycleId; current.Version = 2;
            current.State = prior.State; current.StartDate = prior.StartDate; current.EndDate = prior.EndDate;
            current.ApprovedBy = prior.ApprovedBy; current.ApprovedAt = prior.ApprovedAt; current.IsPublished = true;
            var repository = new NutritionPrescriptionRepository(db);
            await repository.AddAsync(prior, CancellationToken.None);
            await repository.AddAsync(current, CancellationToken.None);
        }
        await using (var db = Context())
        {
            var duplicate = Prescription(patientId); duplicate.CycleId = cycleId; duplicate.Version = 3;
            duplicate.State = "aprobado"; duplicate.StartDate = new(2026, 11, 1); duplicate.EndDate = new(2026, 11, 30);
            duplicate.ApprovedBy = Guid.NewGuid(); duplicate.ApprovedAt = DateTime.UtcNow; duplicate.IsPublished = true;
            await Assert.ThrowsAsync<DbUpdateException>(() => new NutritionPrescriptionRepository(db).AddAsync(duplicate, CancellationToken.None));
        }
        await using var verify = Context();
        Assert.Equal(2, await verify.NutritionPrescriptions.CountAsync(x => x.State == "aprobado"));
        Assert.Equal(1, await verify.NutritionPrescriptions.CountAsync(x => x.IsPublished));
    }

    [NutritionPostgresFact]
    public async Task Migration_CleanDatabase_ExactCatalogIdempotent_AndReversibleWithoutLegacyChanges()
    {
        await using var db = Context();
        var repository = new NutritionPrescriptionRepository(db); var foods = await repository.ListFoodsAsync(CancellationToken.None);
        Assert.Equal(35, foods.Count);
        var oats = Assert.Single(foods, x => x.Id == "avena");
        Assert.Equal(379, oats.Calories100); Assert.Equal(67.7, oats.Carbs100); Assert.Equal(13.2, oats.Protein100);
        Assert.Equal(6.5, oats.Fat100); Assert.Equal(10.1, oats.Fiber100); Assert.Null(oats.UnitStep);
        var oil = Assert.Single(foods, x => x.Id == "aceite"); Assert.Equal(4.5, oil.GramsPerUnit); Assert.Equal(.5, oil.UnitStep);
        using var stream = typeof(NutritionPrescriptionRepository).Assembly.GetManifestResourceStream("CoppAddresd.Infrastructure.Migrations.Seed.AddNutritionFoodCatalog.sql")!;
        using var reader = new StreamReader(stream); var seed = await reader.ReadToEndAsync();
        await db.Database.ExecuteSqlRawAsync(seed); await db.Database.ExecuteSqlRawAsync(seed);
        Assert.Equal(35, await db.NutritionFoodItems.CountAsync());
        var patient = await PatientAsync(db); var legacy = await DraftAsync(db, patient.Id);
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var catalogMigration = migrations.FindIndex(x => x.EndsWith("_AddNutritionPrescriptions"));
        Assert.True(catalogMigration > 0);
        await db.GetService<IMigrator>().MigrateAsync(migrations[catalogMigration - 1]);
        Assert.True(await db.NutritionPlans.AnyAsync(x => x.Id == legacy.Id));
        await db.Database.MigrateAsync(); Assert.Equal(35, await db.NutritionFoodItems.CountAsync());
    }
}
