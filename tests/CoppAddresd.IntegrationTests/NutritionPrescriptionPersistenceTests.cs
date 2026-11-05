using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task Foods_SeededCatalog_MapsRealPostgresForBothPermissions_WithoutPatientScope()
    {
        await using var db = Context(); var repository = new NutritionPrescriptionRepository(db);
        var handler = new CoppAddresd.Application.Features.Nutrition.Queries.NutritionFoodCatalogQueryHandler(repository);
        var actor = new CoppAddresd.Application.Features.Nutrition.Cases.NutritionActor(Guid.NewGuid(),null,false,true,false);
        var foods = await handler.Handle(new(actor),CancellationToken.None);
        Assert.Equal(35,foods.Count); Assert.Equal(foods.Select(x => x.Code).Order(StringComparer.Ordinal),foods.Select(x => x.Code));
        Assert.Equal(new[]{"C","F","P","V"},foods.Select(x => x.Role).Distinct().Order());
        var oats = Assert.Single(foods,x => x.Code=="avena");
        Assert.Equal("Avena en hojuelas (seca)",oats.Name); Assert.Equal("Carbohidratos",oats.Group);
        Assert.Equal(379,oats.Kcal100); Assert.Equal(67.7,oats.Carbs100); Assert.Equal(13.2,oats.Protein100);
        Assert.Equal(6.5,oats.Fat100); Assert.Equal(10.1,oats.Fiber100); Assert.Null(oats.HouseholdEquivalent);
        var oil = Assert.Single(foods,x => x.Code=="aceite"); Assert.Equal(884,oil.Kcal100); Assert.Equal("Grasas",oil.Group);
        Assert.Equal(new CoppAddresd.Application.Features.Nutrition.Queries.NutritionHouseholdEquivalent("cucharadita","cucharaditas",4.5,.5),oil.HouseholdEquivalent);
        Assert.Equal(foods,await handler.Handle(new(actor with { CanManage=false,CanApprove=true }),CancellationToken.None));
        Assert.Empty(db.ChangeTracker.Entries());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ListFoodsAsync(cancel.Token));
    }

    [NutritionPostgresFact]
    public async Task SelfService_PublishedSelection_LocalBoundaries_Ties_AndLegacyCoexistence()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var other = await PatientAsync(db);
        patient.Timezone = "America/Bogota"; await db.SaveChangesAsync();
        var today = new DateOnly(2026, 11, 5);
        var repository = new NutritionPrescriptionRepository(db);
        async Task<NutritionPrescription> Add(Guid patientId, DateOnly start, string id)
        {
            var p = Prescription(patientId); p.Id = Guid.Parse(id); p.State = "aprobado"; p.IsPublished = true;
            p.StartDate = start; p.EndDate = start.AddDays(29); p.ApprovedBy = p.CreatedBy; p.ApprovedAt = DateTime.UtcNow;
            await repository.AddAsync(p, CancellationToken.None); return p;
        }
        await Add(patient.Id, today.AddDays(-29), "10000000-0000-0000-0000-000000000001");
        await Add(patient.Id, today.AddDays(-1), "20000000-0000-0000-0000-000000000001");
        var winner = await Add(patient.Id, today.AddDays(-1), "30000000-0000-0000-0000-000000000001");
        await Add(patient.Id, today.AddDays(1), "40000000-0000-0000-0000-000000000001");
        await Add(other.Id, today, "50000000-0000-0000-0000-000000000001");
        await Add(patient.Id, today.AddDays(-30), "60000000-0000-0000-0000-000000000001");
        await repository.AddAsync(Prescription(patient.Id), CancellationToken.None);
        var selected = (await repository.GetCurrentPublishedAsync(patient.Id, today, CancellationToken.None))!;
        Assert.Equal(winner.Id, selected.Id);
        Assert.NotNull(Assert.Single(Assert.Single(selected.Meals).Items).FoodItem);
        var legacy = await DraftAsync(db, patient.Id); legacy.ReviewStatus = "approved";
        legacy.ApprovedBy = Guid.NewGuid(); legacy.ApprovedAt = DateTime.UtcNow;
        db.NutritionPlanAssignments.Add(new() { PatientId = patient.Id, PlanId = legacy.Id,
            StartDate = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc), Status = AssignmentStatus.Active });
        await db.SaveChangesAsync();
        var patients = NSubstitute.Substitute.For<CoppAddresd.Application.Interfaces.IPatientRepository>();
        patients.GetByUserIdAsync(patient.UserId!.Value, NSubstitute.Arg.Any<CancellationToken>()).Returns(patient);
        var query = new CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan.GetMyNutritionPlanQueryHandler(patients,
            new WellnessRepository(db), NullLogger<CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan.GetMyNutritionPlanQueryHandler>.Instance,
            repository, new NutritionClock(new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero)));
        var result = (await query.Handle(new(patient.UserId.Value), CancellationToken.None))!;
        Assert.Equal(winner.Id, result.Id); Assert.Equal(2, result.CurrentDay); Assert.Equal(30, result.DurationDays);
        Assert.Equal(30, (await repository.GetCurrentPublishedAsync(patient.Id, today.AddDays(30), CancellationToken.None))!.EndDate!.Value.DayNumber
            - today.AddDays(1).DayNumber + 1);
        Assert.Null(await repository.GetCurrentPublishedAsync(patient.Id, today.AddDays(31), CancellationToken.None));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetCurrentPublishedAsync(patient.Id, today, cancel.Token));
    }

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
