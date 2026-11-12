using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private NutritionDraftWorkflow DraftPipeline(CoppAddresd.Infrastructure.Persistence.AppDbContext db)
        => new(new NutritionDraftReservationStore(db), new ProgramAssignmentRepository(db), Cases(db),
            NullLogger<NutritionDraftWorkflow>.Instance, TimeProvider.System);

    private async Task<(Guid PatientId, Guid BatteryId)> ReadyDraftPatientAsync(CoppAddresd.Infrastructure.Persistence.AppDbContext db)
    {
        var patient = await PatientAsync(db); patient.DateOfBirth = new DateTime(1988, 1, 1, 0, 0, 0, DateTimeKind.Utc); patient.Gender = "F";
        var battery = await db.HealthTestBatteries.Include(x => x.Items).SingleAsync(x => x.Code == "bateria-inicial");
        var group = new HealthTestBatteryAssignment { Id = Guid.NewGuid(), PatientId = patient.Id, BatteryId = battery.Id,
            Status = HealthTestAssignmentStatus.completed };
        foreach (var item in battery.Items.Where(x => x.IsRequired))
        {
            var versionId = item.VersionId!.Value;
            var assignment = new HealthTestAssignment { Id = Guid.NewGuid(), PatientId = patient.Id,
                VersionId = versionId, Status = HealthTestAssignmentStatus.completed };
            assignment.Evaluations.Add(new HealthTestEvaluation { Id = Guid.NewGuid(), PatientId = patient.Id,
                VersionId = versionId, Status = HealthTestEvaluationStatus.completed, CompletedAt = DateTime.UtcNow });
            group.Assignments.Add(assignment);
        }
        db.HealthTestBatteryAssignments.Add(group);
        await db.SaveChangesAsync();
        clinical.ConsolidateAsync(patient.Id, Arg.Any<CancellationToken>()).Returns(new ClinicalContextDto(new(38, "F", null),
            [new("weight", 68.5m, "kg", DateTime.UtcNow), new("height", 161, "cm", DateTime.UtcNow)], [], [], [], new(null, null, null)));
        return (patient.Id, group.Id);
    }

    [NutritionPostgresFact]
    public async Task DraftGeneration_ReplayRetryReadyAndInbox_AreDurable()
    {
        await using var db=Context(); var ids=await ReadyDraftPatientAsync(db);
        var access=new NutritionCaseStore(db); var reservations=new NutritionDraftReservationStore(db);
        var handler=new NutritionDraftGenerationCommandHandler(access,new ProgramAssignmentRepository(db),reservations,TimeProvider.System);
        var request=new NutritionDraftGenerationCommand(ids.PatientId,InboxGlobal);
        var first=await handler.Handle(request,CancellationToken.None);
        Assert.Equal("pending",first.State); Assert.Null(first.PrescriptionId);
        Assert.Equal(first,await handler.Handle(request,CancellationToken.None));
        Assert.Equal(1,await db.NutritionDraftReservations.CountAsync()); Assert.False(await db.NutritionPrescriptions.AnyAsync());
        var row=Assert.Single((await Inbox(db).Handle(new(InboxGlobal),CancellationToken.None)).Items);
        Assert.Equal("pending",row.GenerationState); Assert.False(row.GenerationFallback); Assert.NotNull(row.GenerationUpdatedAt);
        await Assert.ThrowsAsync<CoppAddresd.Domain.Exceptions.NotFoundException>(()=>handler.Handle(request with { Actor=InboxGlobal with { ViewAll=false } },CancellationToken.None));
        await db.NutritionDraftReservations.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"failed").SetProperty(x=>x.Attempts,5));
        Assert.Equal("pending",(await handler.Handle(request,CancellationToken.None)).State);
        Assert.Equal(5,(await db.NutritionDraftReservations.AsNoTracking().SingleAsync()).Attempts);
        Assert.True(await DraftPipeline(db).ProcessNextAsync(CancellationToken.None));
        var ready=await handler.Handle(request,CancellationToken.None);
        Assert.Equal("ready",ready.State); Assert.Equal(first.ReservationId,ready.PrescriptionId);
        Assert.Equal(1,await db.NutritionPrescriptions.CountAsync());
        row=Assert.Single((await Inbox(db).Handle(new(InboxGlobal),CancellationToken.None)).Items);
        Assert.Equal("ready",row.GenerationState); Assert.True(row.GenerationFallback);
    }

    [NutritionPostgresFact]
    public async Task DraftPipeline_ReplayAndRestart_ProduceExactlyOneSystemDraftWithFallback()
    {
        await using var db = Context(); var ids = await ReadyDraftPatientAsync(db); var pipeline = DraftPipeline(db);
        var first = await pipeline.QueueAsync(ids.PatientId, ids.BatteryId, CancellationToken.None);
        var replay = await pipeline.QueueAsync(ids.PatientId, ids.BatteryId, CancellationToken.None);
        Assert.NotNull(first); Assert.Equal(first.Id, replay!.Id);
        Assert.True(await pipeline.ProcessNextAsync(CancellationToken.None));
        Assert.False(await pipeline.ProcessNextAsync(CancellationToken.None));
        var reservation = await db.NutritionDraftReservations.AsNoTracking().SingleAsync();
        Assert.Equal("ready", reservation.State); Assert.True(reservation.Fallback);
        var plan = await db.NutritionPrescriptions.AsNoTracking().SingleAsync();
        Assert.Equal(first.Id, plan.Id); Assert.Equal("aprobado", plan.State); Assert.True(plan.IsPublished);
        Assert.Equal(NutritionSystemActor.Id, plan.ApprovedBy);
        Assert.Equal(29, plan.EndDate!.Value.DayNumber - plan.StartDate!.Value.DayNumber);
        Assert.Equal(NutritionSystemActor.Id, plan.CreatedBy);
        var history = await db.NutritionPrescriptionEvents.AsNoTracking().OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.All(history, e => Assert.Equal("system:nutrition", e.Comment));
        Assert.True(history.Single(e => e.Action == "publish").AutoApproved);
        Assert.Contains("IMPORTANT DISCLAIMER", history.Single(e => e.Action == "publish").SnapshotJson);
        // Reinicio entre commit del plan y confirmación de la reserva: la correlación fija reutiliza el plan.
        await db.NutritionDraftReservations.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "pending"));
        await using var restarted = Context();
        Assert.True(await DraftPipeline(restarted).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(1, await restarted.NutritionPrescriptions.CountAsync());
        Assert.Equal(2, await restarted.NutritionPrescriptionEvents.CountAsync());
        Assert.Equal(120, await restarted.NutritionPrescriptionMeals.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task DraftReservation_ConcurrentReplayAndClaims_AreUniqueAndRecoverExpiredLease()
    {
        Guid patientId, batteryId;
        await using (var db = Context()) { var ids = await ReadyDraftPatientAsync(db); patientId = ids.PatientId; batteryId = ids.BatteryId; }
        async Task<Guid> Reserve()
        {
            await using var db = Context();
            return (await new NutritionDraftReservationStore(db).ReserveAsync(patientId, batteryId, CancellationToken.None)).Id;
        }
        Assert.Single((await Task.WhenAll(Reserve(), Reserve())).Distinct());
        var now = DateTime.UtcNow.AddSeconds(1);
        async Task<NutritionDraftReservation?> Claim()
        {
            await using var db = Context(); return await new NutritionDraftReservationStore(db).ClaimAsync(now, CancellationToken.None);
        }
        var claimed = Assert.Single(await Task.WhenAll(Claim(), Claim()), x => x is not null)!;
        await using var recovery = Context(); var store = new NutritionDraftReservationStore(recovery);
        Assert.Null(await store.ClaimAsync(now.AddMinutes(1), CancellationToken.None));
        var reclaimed = await store.ClaimAsync(now.AddMinutes(4), CancellationToken.None);
        Assert.NotNull(reclaimed); Assert.Equal(claimed.Id, reclaimed.Id); Assert.NotEqual(claimed.ClaimToken, reclaimed.ClaimToken);
        await store.CompleteAsync(claimed, claimed.Id, false, CancellationToken.None);
        Assert.Equal("generating", (await recovery.NutritionDraftReservations.AsNoTracking().SingleAsync()).State);
    }

    [NutritionPostgresFact]
    public async Task DraftPipeline_FailureIsVisibleAndCancellationDoesNotBecomeFailure()
    {
        await using var db = Context(); var ids = await ReadyDraftPatientAsync(db);
        clinical.ConsolidateAsync(ids.PatientId, Arg.Any<CancellationToken>()).Returns(new ClinicalContextDto(new(38, "F", null), [], [], [], [], new(null, null, null)));
        var pipeline = DraftPipeline(db);
        await pipeline.QueueAsync(ids.PatientId, ids.BatteryId, CancellationToken.None);
        Assert.True(await pipeline.ProcessNextAsync(CancellationToken.None));
        Assert.Equal("failed", (await db.NutritionDraftReservations.AsNoTracking().SingleAsync()).State);
        Assert.Empty(await db.NutritionPrescriptions.ToListAsync());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.ProcessNextAsync(cancellation.Token));
    }

    [NutritionPostgresFact]
    public async Task DraftPipeline_ReconcilesLostEvents_AndSuppressesLegacyProducer()
    {
        await using var db = Context(); var ids = await ReadyDraftPatientAsync(db);
        var health = new HealthTestRepository(db);
        var baseline = await health.ListAssignmentsByPatientAsync(ids.PatientId, CancellationToken.None);
        for (var i=0; i<12; i++) db.HealthTestAssignments.Add(new HealthTestAssignment
        {
            Id=Guid.NewGuid(), PatientId=ids.PatientId, VersionId=baseline[i%baseline.Count].VersionId,
            Status=HealthTestAssignmentStatus.pending,
        });
        await db.SaveChangesAsync();
        var all = await health.ListAssignmentsByPatientAsync(ids.PatientId, CancellationToken.None);
        Assert.Equal(21, all.Count);
        var initial = InitialAssessmentSelection.Select(await health.GetAutoAssignBatteryAsync(CancellationToken.None),
            await health.ListBatteryAssignmentsByPatientAsync(ids.PatientId, CancellationToken.None), all);
        Assert.Equal(9, initial.Count);
        Assert.All(initial, row => Assert.Equal(ids.BatteryId, row.BatteryAssignmentId));
        var actor = Substitute.For<CoppAddresd.Api.Context.IProgramActorContext>();
        actor.ResolvePatientProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)ids.PatientId);
        var controller = new CoppAddresd.Api.Controllers.HealthTestsMeController(Substitute.For<MediatR.IMediator>(), actor, health);
        var response = await controller.GetMyAssignments(CancellationToken.None, "initial");
        var dto = Assert.IsAssignableFrom<IReadOnlyList<HealthTestAssignmentDto>>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value);
        Assert.All(dto, row => { Assert.True(row.IsRequired); Assert.Equal(9, row.BatteryRequiredCount); });
        var configured = await health.GetAutoAssignBatteryAsync(CancellationToken.None);
        var optional = await db.HealthTestBatteryItems.FirstAsync(x => x.BatteryId == configured!.Id);
        optional.IsRequired = false;
        var optionalAssignment = await db.HealthTestAssignments.SingleAsync(x => x.BatteryAssignmentId == ids.BatteryId && x.Version!.InstrumentId == optional.InstrumentId);
        optionalAssignment.Status = HealthTestAssignmentStatus.pending;
        await db.SaveChangesAsync();
        response = await controller.GetMyAssignments(CancellationToken.None, "initial");
        dto = Assert.IsAssignableFrom<IReadOnlyList<HealthTestAssignmentDto>>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result).Value);
        Assert.All(dto, row => Assert.Equal(8, row.BatteryRequiredCount));
        Assert.False(dto.Single(x => x.Id == optionalAssignment.Id).IsRequired);
        // Incluso si el worker legacy corre antes del evento, no debe reservar otro plan.
        await Workflow(db).QueueIfReadyAsync(ids.PatientId, CancellationToken.None);
        await Workflow(db).ReconcileAsync(CancellationToken.None);
        Assert.Empty(await db.NutritionPlans.ToListAsync());
        await DraftPipeline(db).ReconcileAsync(CancellationToken.None);
        Assert.Single(await db.NutritionDraftReservations.ToListAsync());
        await Workflow(db).QueueIfReadyAsync(ids.PatientId, CancellationToken.None);
        await Workflow(db).ReconcileAsync(CancellationToken.None);
        Assert.Empty(await db.NutritionPlans.ToListAsync());
    }

    [NutritionPostgresFact]
    public async Task DraftReservation_AuditUsesTechnicalGuc_AndDoesNotLeakAcrossTransactions()
    {
        await using var seed = Context(); var ids = await ReadyDraftPatientAsync(seed);
        var audit = new CoppAddresd.Infrastructure.Persistence.AuditTriggerInterceptor(
            new CoppAddresd.Infrastructure.Services.HttpAuditActorContext(new Microsoft.AspNetCore.Http.HttpContextAccessor()));
        await using var db = new CoppAddresd.Infrastructure.Persistence.AppDbContext(
            new DbContextOptionsBuilder<CoppAddresd.Infrastructure.Persistence.AppDbContext>()
                .UseNpgsql(connection).AddInterceptors(audit).Options);
        using (NutritionSystemActor.Begin())
            await new NutritionDraftReservationStore(db).ReserveAsync(ids.PatientId, ids.BatteryId, CancellationToken.None);
        var log = await db.ActivityLogs.AsNoTracking().SingleAsync(x => x.TableName == "nutrition_draft_reservations");
        Assert.Equal(CoppAddresd.Domain.Enums.AuditActorType.System, log.ActorType);
        Assert.Equal("system:nutrition", log.UserRole); Assert.Null(log.UserId);
        await using var transaction = await db.Database.BeginTransactionAsync();
        Assert.Equal("", await db.Database.SqlQueryRaw<string>("SELECT coalesce(current_setting('audit.user_role', true), '') AS \"Value\"").SingleAsync());
        await transaction.RollbackAsync();
    }
}
