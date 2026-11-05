using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private sealed class NutritionClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [NutritionPostgresFact]
    public async Task State_RejectsInvalidTransitionsAndEmptyReturn_ConfirmsCriticalAlertsWithActor()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var cases = Cases(db); var actor = NutritionAdmin;
        var input = CompleteNutritionInput with { Manual = CompleteNutritionInput.Manual! with { SleepRF = 2, RiesgoNum = 4, NutRiesgo = "alto", Hba1c = 6.5 } };
        var plan = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, input), CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, actor), CancellationToken.None));
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Submit, actor), CancellationToken.None);
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Return, actor, comment: " "), CancellationToken.None));
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Return, actor, comment: "Revisar clínica"), CancellationToken.None);
        Assert.Equal("devuelto", plan.State);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Submit, actor), CancellationToken.None);
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, actor), CancellationToken.None));
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Save, actor), CancellationToken.None));
        var events = await db.NutritionPrescriptionEvents.AsNoTracking().Where(e => e.Action == "approve").ToListAsync();
        var approved = Assert.Single(events); Assert.True(approved.CriticalConfirmed); Assert.True(approved.CriticalCount >= 4);
        Assert.Equal(actor.UserId, approved.ActorUserId); Assert.True(approved.AutoApproved);
    }

    [NutritionPostgresFact]
    public async Task State_LocalCycleHasThirtyDays_RenewalAtFiveDays_AndOverlapsAreRejected()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var actor = NutritionAdmin;
        var time = new NutritionClock(DateTimeOffset.Parse("2026-11-06T02:00:00Z")); var cases = Cases(db, time: time);
        var plan = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Submit, actor), CancellationToken.None);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 11, 5), plan.StartDate); Assert.Equal(new DateOnly(2026, 12, 4), plan.EndDate);
        var other = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        other = await cases.ExecuteAsync(Mutation(other, NutritionOperation.Submit, actor), CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => cases.ExecuteAsync(Mutation(other, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None));
        time.Now = DateTimeOffset.Parse("2026-11-29T17:00:00Z");
        Assert.Equal("por_renovar", (await cases.GetAsync(new(patient.Id, plan.Id, actor), CancellationToken.None)).State);
        time.Now = DateTimeOffset.Parse("2026-12-05T17:00:00Z");
        Assert.Equal("por_renovar", (await cases.GetAsync(new(patient.Id, plan.Id, actor), CancellationToken.None)).State);
    }

    [NutritionPostgresFact]
    public async Task State_NineCompletedTestsOfferRegeneration_AndConfirmedEditsSurvive()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var actor = NutritionAdmin; var cases = Cases(db);
        var plan = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        Assert.True(plan.Provisional); Assert.False(plan.CanRegenerate);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Save, actor,
            new(Instructions: "Conservar indicación manual", ConfirmManualEdits: true)), CancellationToken.None);
        var portions = plan.Meals.SelectMany(m => m.Items).Select(i => (i.FoodItemId, i.Grams)).ToArray();
        for (var i = 0; i < 9; i++) await BatteryAsync(db, patient.Id, true);
        Assert.True((await new NutritionCaseStore(db).GetContextAsync(patient.Id, CancellationToken.None))!.TestsComplete);
        var ready = await cases.GetAsync(new(patient.Id, plan.Id, actor), CancellationToken.None);
        Assert.True(ready.CanRegenerate); Assert.Equal("Conservar indicación manual", ready.Instructions);
        var regenerated = await cases.ExecuteAsync(Mutation(ready, NutritionOperation.Regenerate, actor), CancellationToken.None);
        Assert.False(regenerated.Provisional); Assert.False(regenerated.CanRegenerate);
        Assert.Equal("Conservar indicación manual", regenerated.Instructions);
        Assert.Equal(portions, regenerated.Meals.SelectMany(m => m.Items).Select(i => (i.FoodItemId, i.Grams)).ToArray());
        Assert.Equal(plan.Targets, regenerated.Targets);
    }

    [NutritionPostgresFact]
    public async Task State_ConcurrentApprovalsPublishOnce()
    {
        Guid patientId; NutritionView submitted; var actor = NutritionAdmin;
        await using (var db = Context())
        {
            patientId = (await PatientAsync(db)).Id; var cases = Cases(db);
            var draft = await cases.ExecuteAsync(new(NutritionOperation.Generate, patientId, null, null, actor, CompleteNutritionInput), CancellationToken.None);
            submitted = await cases.ExecuteAsync(Mutation(draft, NutritionOperation.Submit, actor), CancellationToken.None);
        }
        async Task<bool> Approve()
        {
            await using var db = Context();
            try { await Cases(db).ExecuteAsync(Mutation(submitted, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None); return true; }
            catch (BusinessRuleViolationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Approve(), Approve()), x => x);
        await using var verify = Context();
        Assert.Equal(1, await verify.NutritionPrescriptions.CountAsync(x => x.IsPublished));
        Assert.Equal(1, await verify.NutritionPrescriptionEvents.CountAsync(x => x.Action == "approve"));
    }
}
