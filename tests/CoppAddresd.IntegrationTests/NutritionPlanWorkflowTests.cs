using System.Text.Json;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed class NutritionPostgresFactAttribute : FactAttribute
{
    public NutritionPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION")))
            Skip = "Requiere PostgreSQL aislado mediante COP_TEST_DB_CONNECTION.";
    }
}

/// <summary>Flujo real contra PostgreSQL: ninguna llamada sale al proveedor de IA.</summary>
public sealed partial class NutritionPlanWorkflowTests : IAsyncLifetime
{
    private string connection = "";
    private string database = "";
    private readonly string server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? "";
    private readonly IClinicalContextService clinical = Substitute.For<IClinicalContextService>();
    private readonly ISafetyRulesService safety = Substitute.For<ISafetyRulesService>();
    private readonly IAiServiceClient ai = Substitute.For<IAiServiceClient>();

    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connection, options => options.CommandTimeout(20)).Options);
    private NutritionPlanWorkflow Workflow(AppDbContext db) => new(db, clinical, safety, ai, NullLogger<NutritionPlanWorkflow>.Instance);

    public async Task InitializeAsync()
    {
        if (server.Length == 0) return;
        database = $"copp_nutrition_test_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres" };
        await using (var sql = new NpgsqlConnection(admin.ConnectionString))
        {
            await sql.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" TEMPLATE template0", sql);
            await command.ExecuteNonQueryAsync();
        }
        connection = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS auth; CREATE TABLE auth.\"Users\" (\"Id\" uuid PRIMARY KEY, \"SecurityStamp\" text NULL);");
        await db.Database.MigrateAsync();
        clinical.ConsolidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(
            new ClinicalContextDto(new(38, "F", "Bajo"), [], [], [], [], new(null, null, null)));
        safety.EvaluateAsync(Arg.Any<ClinicalContextDto>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<RestrictionDto>());
        ai.GeneratePlanAsync("nutrition", Arg.Any<ClinicalContextDto>(), Arg.Any<IReadOnlyList<RestrictionDto>>(), Arg.Any<CancellationToken>())
            .Returns(new AiPlanResult("nutrition", Payload()));
    }

    public async Task DisposeAsync()
    {
        if (database.Length == 0) return;
        NpgsqlConnection.ClearAllPools();
        await using var sql = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await sql.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<PatientProfile> PatientAsync(AppDbContext db, Guid? id = null)
    {
        var patient = new PatientProfile { Id = id ?? Guid.NewGuid(), FirstName = "Paciente", LastName = "Prueba", Status = "Activo", UserId = Guid.NewGuid() };
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"Users\" (\"Id\") VALUES ({patient.UserId.Value})");
        db.PatientProfiles.Add(patient); await db.SaveChangesAsync(); return patient;
    }

    private async Task<HealthTestAssignment> BatteryAsync(AppDbContext db, Guid patientId, bool completed)
    {
        var instrument = new HealthTestInstrument { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Hábitos alimentarios" };
        var version = new HealthTestVersion { Id = Guid.NewGuid(), Instrument = instrument, VersionNumber = 1 };
        var battery = new HealthTestBattery { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Batería inicial" };
        battery.Items.Add(new HealthTestBatteryItem { Id = Guid.NewGuid(), Instrument = instrument, Version = version, IsRequired = true });
        var group = new HealthTestBatteryAssignment { Id = Guid.NewGuid(), PatientId = patientId, Battery = battery,
            Status = completed ? HealthTestAssignmentStatus.completed : HealthTestAssignmentStatus.pending };
        var assignment = new HealthTestAssignment { Id = Guid.NewGuid(), PatientId = patientId, BatteryAssignment = group, Version = version,
            Status = completed ? HealthTestAssignmentStatus.completed : HealthTestAssignmentStatus.pending };
        db.HealthTestAssignments.Add(assignment); await db.SaveChangesAsync();
        if (completed)
        {
            var evaluation = new HealthTestEvaluation { Id = Guid.NewGuid(), PatientId = patientId, AssignmentId = assignment.Id, VersionId = version.Id,
                Status = HealthTestEvaluationStatus.completed, CompletedAt = DateTime.UtcNow };
            evaluation.Results.Add(new HealthTestResult { Id = Guid.NewGuid(), Code = "nutrition_habits", Label = "Hábitos", Value = 42,
                ResultType = HealthTestResultType.score, Severity = HealthTestSeverity.moderate });
            db.HealthTestEvaluations.Add(evaluation); await db.SaveChangesAsync();
        }
        return assignment;
    }

    private static JsonElement Payload() => JsonSerializer.SerializeToElement(new
    {
        name = "Menú semanal revisable", duration_days = 30, daily_calorie_target = 1800,
        days = Enumerable.Range(1, 7).SelectMany(day => new[] { "Desayuno", "Almuerzo", "Merienda", "Cena" }
            .Select(meal => new { day_number = day, meal_type = meal, foods = "Menú de prueba", description = "Porción revisable", calories = 400 })),
    });

    private async Task<NutritionPlan> DraftAsync(AppDbContext db, Guid patientId)
    {
        var plan = new NutritionPlan { Id = Guid.NewGuid(), PatientId = patientId, IsTemplate = false, Name = "Borrador", DurationDays = 30 };
        NutritionPlanWorkflow.ApplyGeneratedPlan(plan, Payload());
        db.NutritionPlans.Add(plan); await db.SaveChangesAsync(); return plan;
    }

    [NutritionPostgresFact]
    public async Task AllBatteriesRequired_QueueIsDurableAndIdempotentAcrossContexts()
    {
        await using var db = Context(); var patient = await PatientAsync(db);
        await BatteryAsync(db, patient.Id, true); var pending = await BatteryAsync(db, patient.Id, false);
        await Workflow(db).QueueIfReadyAsync(patient.Id, CancellationToken.None);
        Assert.Empty(await db.NutritionPlans.ToListAsync());
        pending.Status = HealthTestAssignmentStatus.completed; await db.SaveChangesAsync();
        await using var second = Context();
        await Task.WhenAll(Workflow(db).QueueIfReadyAsync(patient.Id, CancellationToken.None), Workflow(second).QueueIfReadyAsync(patient.Id, CancellationToken.None));
        Assert.Single(await db.NutritionPlans.AsNoTracking().ToListAsync());
        Assert.Single(await db.NutritionPlanReviewEvents.AsNoTracking().ToListAsync());
    }

    [NutritionPostgresFact]
    public async Task RealGenerationReceivesResults_AndDraftNeverAppearsToPatient()
    {
        await using var db = Context(); var patient = await PatientAsync(db); await BatteryAsync(db, patient.Id, true);
        var workflow = Workflow(db); await workflow.QueueIfReadyAsync(patient.Id, CancellationToken.None);
        Assert.True(await workflow.ProcessNextAsync(CancellationToken.None));
        var plan = await db.NutritionPlans.AsNoTracking().Include(p => p.Days).SingleAsync();
        Assert.Equal("completed", plan.GenerationStatus); Assert.Equal("pending_review", plan.ReviewStatus);
        Assert.Equal(28, plan.Days.Count); Assert.Null(plan.ApprovedAt);
        Assert.Empty(await db.NutritionPlanAssignments.ToListAsync());
        await ai.Received(1).GeneratePlanAsync("nutrition", Arg.Is<ClinicalContextDto>(c => c.Assessments != null && c.Assessments.Any(a => a.Code == "nutrition_habits" && a.Value == 42)), Arg.Any<IReadOnlyList<RestrictionDto>>(), Arg.Any<CancellationToken>());
        Assert.Equal("pending_review", (await workflow.GetStatusAsync(patient.UserId!.Value, "America/Bogota", CancellationToken.None)).Status);
        var patients = Substitute.For<IPatientRepository>(); patients.GetByUserIdAsync(patient.UserId.Value, Arg.Any<CancellationToken>()).Returns(patient);
        var query = new GetMyNutritionPlanQueryHandler(patients, new WellnessRepository(db), NullLogger<GetMyNutritionPlanQueryHandler>.Instance);
        Assert.Null(await query.Handle(new(patient.UserId.Value), CancellationToken.None));
    }

    [NutritionPostgresFact]
    public async Task ApprovalPublishesThirtyDays_AndEditingRevokesPublicationWithActorHistory()
    {
        Guid planId, patientId, actorId = Guid.NewGuid(); int revision;
        await using (var db = Context())
        {
            var patient = await PatientAsync(db); patientId = patient.Id; await BatteryAsync(db, patient.Id, true);
            var plan = await DraftAsync(db, patient.Id); planId = plan.Id;
            var approved = Assert.Single(await Workflow(db).ApproveAsync([new(plan.Id, plan.Revision, "Revisado")], new(actorId, null, true), CancellationToken.None));
            revision = approved.Revision; Assert.Equal(actorId, approved.ApprovedBy);
            var status = await Workflow(db).GetStatusAsync(patient.UserId!.Value, "UTC", CancellationToken.None);
            Assert.Equal("active", status.Status); Assert.Equal(revision, status.Revision);
            Assert.Equal(1, status.CurrentDay);
            var assignment = await db.NutritionPlanAssignments.SingleAsync();
            Assert.Equal(29, (assignment.EndDate!.Value - assignment.StartDate).Days);
        }
        await using (var db = Context())
        {
            var plan = (await new WellnessRepository(db).GetPlanByIdAsync(planId))!;
            var request = new UpdateNutritionPlanRequest("Menú ajustado", plan.Description, plan.TargetCondition, 30,
                plan.DailyCalorieTarget, plan.DailyProteinTarget, plan.DailyCarbsTarget, plan.DailyFatTarget, plan.DailyFiberTarget,
                plan.Allergens, plan.MealTiming, NutritionPlanStatus.Active, null, revision);
            var handler = new UpdateNutritionPlanCommandHandler(new WellnessRepository(db), NullLogger<UpdateNutritionPlanCommandHandler>.Instance);
            var updated = await handler.Handle(new(planId, request, actorId), CancellationToken.None);
            Assert.Equal("pending_review", updated!.ReviewStatus); Assert.Null(updated.ApprovedAt);
            var events = await Workflow(db).HistoryAsync(planId, new(actorId, null, true), CancellationToken.None);
            Assert.Contains(events, e => e.Action == "approved" && e.ActorUserId == actorId);
            Assert.Contains(events, e => e.Action == "modified" && e.ActorUserId == actorId && e.Revision > revision);
        }
    }

    [NutritionPostgresFact]
    public async Task BulkApproval_StaleRevision_DoesNotPublishAnyPlan()
    {
        await using var db = Context(); var first = await PatientAsync(db); var second = await PatientAsync(db);
        await BatteryAsync(db, first.Id, true); await BatteryAsync(db, second.Id, true);
        var a = await DraftAsync(db, first.Id); var b = await DraftAsync(db, second.Id);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Workflow(db).ApproveAsync([new(a.Id, 1), new(b.Id, 99)], new(Guid.NewGuid(), null, true), CancellationToken.None));
        Assert.Empty(await db.NutritionPlanAssignments.ToListAsync());
        Assert.All(await db.NutritionPlans.AsNoTracking().ToListAsync(), p => Assert.Null(p.ApprovedAt));
    }

    [NutritionPostgresFact]
    public async Task ConcurrentApproval_PublishesOnceAndKeepsOneActiveCycle()
    {
        Guid id;
        await using (var db = Context())
        {
            var patient = await PatientAsync(db); await BatteryAsync(db, patient.Id, true); id = (await DraftAsync(db, patient.Id)).Id;
        }
        async Task<bool> Attempt()
        {
            await using var db = Context();
            try { await Workflow(db).ApproveAsync([new(id, 1)], new(Guid.NewGuid(), null, true), CancellationToken.None); return true; }
            catch (BusinessRuleViolationException) { return false; }
        }
        var attempts = await Task.WhenAll(Attempt(), Attempt()); Assert.Single(attempts, success => success);
        await using var verify = Context();
        Assert.Single(await verify.NutritionPlanAssignments.Where(a => a.Status == AssignmentStatus.Active).ToListAsync());
        Assert.Single(await verify.NutritionPlanReviewEvents.Where(e => e.Action == "approved").ToListAsync());
    }

    [NutritionPostgresFact]
    public async Task ExpiredCycle_QueuesNextDraft_WithoutReusingOldMenu()
    {
        await using var db = Context(); var patient = await PatientAsync(db); await BatteryAsync(db, patient.Id, true);
        var plan = await DraftAsync(db, patient.Id); var workflow = Workflow(db);
        await workflow.ApproveAsync([new(plan.Id, 1)], new(Guid.NewGuid(), null, true), CancellationToken.None);
        var assignment = await db.NutritionPlanAssignments.SingleAsync(); assignment.StartDate = DateTime.UtcNow.Date.AddDays(-31); assignment.EndDate = assignment.StartDate.AddDays(29);
        await db.SaveChangesAsync(); await workflow.ReconcileAsync(CancellationToken.None);
        Assert.Equal(2, await db.NutritionPlans.CountAsync());
        Assert.Equal("generating", (await workflow.GetStatusAsync(patient.UserId!.Value, "America/Bogota", CancellationToken.None)).Status);
    }

    [NutritionPostgresFact]
    public async Task Reconcile_PartialPatientsDoNotStarveReadyPatient_AndLegacyArchiveDoesNotBlock()
    {
        await using var db = Context();
        for (var index = 1; index <= 25; index++)
        {
            var patient = await PatientAsync(db, Guid.Parse($"00000000-0000-0000-0000-{index:D12}"));
            await BatteryAsync(db, patient.Id, true); await BatteryAsync(db, patient.Id, false);
        }
        var ready = await PatientAsync(db, Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));
        await BatteryAsync(db, ready.Id, true);
        var archived = await DraftAsync(db, ready.Id); archived.Status = NutritionPlanStatus.Archived;
        await db.SaveChangesAsync(); await Workflow(db).ReconcileAsync(CancellationToken.None);
        var queued = Assert.Single(await db.NutritionPlans.AsNoTracking().Where(p => p.GenerationStatus == "queued").ToListAsync());
        Assert.Equal(ready.Id, queued.PatientId);
    }

    [NutritionPostgresFact]
    public async Task BulkApproval_PublishesAllReviewedPatientsAndTracesEachActor()
    {
        await using var db = Context(); var first = await PatientAsync(db); var second = await PatientAsync(db);
        await BatteryAsync(db, first.Id, true); await BatteryAsync(db, second.Id, true);
        var a = await DraftAsync(db, first.Id); var b = await DraftAsync(db, second.Id); var actor = Guid.NewGuid();
        var approved = await Workflow(db).ApproveAsync([new(a.Id, 1), new(b.Id, 1)], new(actor, null, true), CancellationToken.None);
        Assert.Equal(2, approved.Count); Assert.All(approved, plan => Assert.Equal(actor, plan.ApprovedBy));
        Assert.Equal(2, await db.NutritionPlanAssignments.CountAsync(x => x.Status == AssignmentStatus.Active));
        Assert.Equal(2, await db.NutritionPlanReviewEvents.CountAsync(x => x.ActorUserId == actor && x.Action == "approved"));
    }

    [NutritionPostgresFact]
    public async Task CurrentCycle_CannotBeReplacedWithAnotherDraft_AndReapprovalKeepsCycleDates()
    {
        Guid planId, patientId; DateTime start;
        await using (var db = Context())
        {
            var patient = await PatientAsync(db); patientId = patient.Id; await BatteryAsync(db, patient.Id, true);
            var plan = await DraftAsync(db, patient.Id); planId = plan.Id;
            await Workflow(db).ApproveAsync([new(plan.Id, 1)], new(Guid.NewGuid(), null, true), CancellationToken.None);
            var assignment = await db.NutritionPlanAssignments.SingleAsync();
            start = DateTime.UtcNow.Date.AddDays(-8); assignment.StartDate = start; assignment.EndDate = start.AddDays(29);
            await db.SaveChangesAsync();
            var other = await DraftAsync(db, patient.Id);
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() => Workflow(db).ApproveAsync([new(other.Id, 1)], new(Guid.NewGuid(), null, true), CancellationToken.None));
        }
        await using (var db = Context())
        {
            var plan = await db.NutritionPlans.FindAsync(planId); plan!.ReviewStatus = "pending_review";
            plan.ApprovedAt = null; plan.ApprovedBy = null; plan.Status = NutritionPlanStatus.Draft; plan.Revision++;
            await db.SaveChangesAsync();
            await Workflow(db).ApproveAsync([new(plan.Id, plan.Revision)], new(Guid.NewGuid(), null, true), CancellationToken.None);
            var cycle = await db.NutritionPlanAssignments.SingleAsync(a => a.Status == AssignmentStatus.Active);
            Assert.Equal(start, cycle.StartDate); Assert.Equal(start.AddDays(29), cycle.EndDate);
            Assert.Equal(patientId, cycle.PatientId);
        }
    }

    [NutritionPostgresFact]
    public async Task InvalidAiStorageFields_PersistFailureWithoutSavingDirtyMenu()
    {
        await using var db = Context(); var patient = await PatientAsync(db); await BatteryAsync(db, patient.Id, true);
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Payload().GetRawText())!;
        payload["target_condition"] = new string('x', 150);
        ai.GeneratePlanAsync("nutrition", Arg.Any<ClinicalContextDto>(), Arg.Any<IReadOnlyList<RestrictionDto>>(), Arg.Any<CancellationToken>())
            .Returns(new AiPlanResult("nutrition", JsonSerializer.SerializeToElement(payload)));
        await Workflow(db).QueueIfReadyAsync(patient.Id, CancellationToken.None);
        Assert.True(await Workflow(db).ProcessNextAsync(CancellationToken.None));
        var plan = await db.NutritionPlans.AsNoTracking().SingleAsync();
        Assert.Equal("failed", plan.GenerationStatus); Assert.Null(plan.GenerationLeaseUntil);
        Assert.Equal(0, await db.NutritionPlanDays.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task RestartDuringFifthAttempt_ExpiredLeaseBecomesRecoverableFailure()
    {
        await using var db = Context(); var patient = await PatientAsync(db);
        var plan = new NutritionPlan { Id = Guid.NewGuid(), PatientId = patient.Id, IsTemplate = false,
            Name = "Generación interrumpida", DurationDays = 30, GenerationStatus = "generating",
            GenerationAttempts = 5, GenerationLeaseUntil = DateTime.UtcNow.AddMinutes(-1) };
        db.NutritionPlans.Add(plan); await db.SaveChangesAsync();
        Assert.True(await Workflow(db).ProcessNextAsync(CancellationToken.None));
        await db.Entry(plan).ReloadAsync();
        Assert.Equal("failed", plan.GenerationStatus); Assert.Null(plan.GenerationLeaseUntil);
        await ai.DidNotReceiveWithAnyArgs().GeneratePlanAsync(default!, default!, default!, default);
    }

    [NutritionPostgresFact]
    public async Task ReviewerWithoutPatientAssignment_CannotReadOrApprove()
    {
        await using var db = Context(); var patient = await PatientAsync(db); await BatteryAsync(db, patient.Id, true); var plan = await DraftAsync(db, patient.Id);
        var actor = new NutritionReviewActor(Guid.NewGuid(), Guid.NewGuid(), false);
        await Assert.ThrowsAsync<NotFoundException>(() => Workflow(db).ApproveAsync([new(plan.Id, 1)], actor, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Workflow(db).HistoryAsync(plan.Id, actor, CancellationToken.None));
        Assert.Empty(await db.NutritionPlanAssignments.ToListAsync());
    }
}
