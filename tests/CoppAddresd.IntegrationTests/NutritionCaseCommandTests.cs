using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private static NutritionGenerateInput CompleteNutritionInput => new(new NutritionPatient(68.5, 161, 38, "F", 26.4,
        Hba1c: 5.9, Mov: "MARCHA", Objetivo: "perder", NutRiesgo: "moderado", SleepRF: 0,
        SleepNivel: "fragil", Adh: "A3", RiesgoNum: 2, Estres: "moderado"), new DateOnly(1988, 1, 1));
    private NutritionCases Cases(CoppAddresd.Infrastructure.Persistence.AppDbContext db, INutritionCaseStore? store = null,
        INutritionDraftClient? client = null, TimeProvider? time = null)
    {
        if (client is null)
        {
            client = Substitute.For<INutritionDraftClient>();
            client.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                // La red nunca ocurre dentro de una transacción de escritura.
                Assert.Null(db.Database.CurrentTransaction);
                var request = call.Arg<NutritionDraftRequest>();
                return new NutritionDraftResponse(new("Propuesta", "Proposal"), new("Revisar", "Review"), request.Menus, "deterministic_fallback");
            });
        }
        return new(new NutritionPrescriptionRepository(db), store ?? new NutritionCaseStore(db), client,
            clinical, Substitute.For<IProgramAssignmentRepository>(), time ?? TimeProvider.System);
    }
    private static NutritionActor NutritionAdmin => new(Guid.NewGuid(), null, true, true, true);
    private static NutritionCaseCommand Mutation(NutritionView plan, NutritionOperation operation, NutritionActor actor,
        NutritionEdit? edit = null, string? comment = null, bool confirm = false) => new(operation, plan.PatientId,
            plan.Id, plan.Revision, actor, Edit: edit, Comment: comment, CriticalConfirmed: confirm);

    [NutritionPostgresFact]
    public async Task Cqrs_GenerateEditApproveNewVersion_PreservesPublicationAndActorHistory()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var actor = NutritionAdmin; var cases = Cases(db);
        var first = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        Assert.True(first.Provisional); Assert.Equal(120, first.Meals.Count); Assert.False(first.IsPublished);
        var changed = await cases.ExecuteAsync(Mutation(first, NutritionOperation.Save, actor,
            new(Instructions: "Edición profesional", InstructionsEn: "Professional edit", ConfirmManualEdits: true)), CancellationToken.None);
        changed = await cases.ExecuteAsync(Mutation(changed, NutritionOperation.Submit, actor), CancellationToken.None);
        var approved = await cases.ExecuteAsync(Mutation(changed, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None);
        Assert.Equal(actor.UserId, approved.ApprovedBy); Assert.True(approved.IsPublished);
        var version = await cases.ExecuteAsync(Mutation(approved, NutritionOperation.CreateVersion, actor), CancellationToken.None);
        Assert.Equal(2, version.Version); Assert.Equal(approved.CycleId, version.CycleId); Assert.False(version.IsPublished);
        Assert.Equal("Edición profesional", version.Instructions);
        version = await cases.ExecuteAsync(Mutation(version, NutritionOperation.Save, actor, new(Instructions: "Solo borrador")), CancellationToken.None);
        Assert.Equal("Edición profesional", (await cases.GetAsync(new(patient.Id, approved.Id, actor), CancellationToken.None)).Instructions);
        version = await cases.ExecuteAsync(Mutation(version, NutritionOperation.Submit, actor), CancellationToken.None);
        version = await cases.ExecuteAsync(Mutation(version, NutritionOperation.Approve, actor, confirm: true), CancellationToken.None);
        Assert.True(version.IsPublished);
        var prior = await cases.GetAsync(new(patient.Id, approved.Id, actor), CancellationToken.None);
        Assert.Equal("aprobado", prior.State); Assert.False(prior.IsPublished); Assert.Equal("Edición profesional", prior.Instructions);
        Assert.Equal(approved.StartDate, version.StartDate); Assert.Equal(approved.EndDate, version.EndDate);
        var history = await cases.HistoryAsync(new(patient.Id, approved.Id, actor), CancellationToken.None);
        Assert.Contains(history, e => e.Action == "approve" && e.AutoApproved && e.ActorUserId == actor.UserId);
        Assert.Contains(history, e => e.Action == "publication_replaced");
    }

    [NutritionPostgresFact]
    public async Task Cqrs_ApplyDay_ChangesOnlyOwnPlan_AndStaleRevisionCannotWrite()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var actor = NutritionAdmin; var cases = Cases(db);
        var draft = await cases.ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        var applied = await cases.ExecuteAsync(Mutation(draft, NutritionOperation.ApplyDay, actor,
            new(SourceDay: 1, ConfirmManualEdits: true)), CancellationToken.None);
        Assert.Equal(120, applied.Meals.Count);
        var breakfast = applied.Meals.First(m => m.MealType == "des");
        Assert.All(applied.Meals.Where(m => m.MealType == "des"), m => Assert.Equal(breakfast.Items, m.Items));
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => cases.ExecuteAsync(Mutation(draft, NutritionOperation.Save, actor,
            new(Instructions: "Obsoleto")), CancellationToken.None));
        Assert.DoesNotContain(await db.NutritionPrescriptionEvents.ToListAsync(), e => e.SnapshotJson.Contains("Obsoleto"));
    }

    [NutritionPostgresFact]
    public async Task Cqrs_BadLlmMenuFallsBackToDeterministicPlan()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var client = Substitute.For<INutritionDraftClient>();
        client.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            new NutritionDraftResponse(new("Inventado", "Invented"), new("Inventado", "Invented"),
                call.Arg<NutritionDraftRequest>().Menus.Select(m => m with { Items = [new("no-existe", 20)] }).ToArray(), "llm"));
        var plan = await Cases(db, client: client).ExecuteAsync(new(NutritionOperation.Generate, patient.Id, null, null,
            NutritionAdmin, CompleteNutritionInput), CancellationToken.None);
        Assert.Equal("deterministic_fallback", plan.NarrativeSource);
        Assert.DoesNotContain(plan.Meals.SelectMany(m => m.Items), i => i.FoodItemId == "no-existe");
        Assert.Equal(120, plan.Meals.Count);
    }

    [NutritionPostgresFact]
    public async Task Cqrs_AuditFailure_RollsBackAggregateAndChildren()
    {
        await using var db = Context(); var patient = await PatientAsync(db); var store = new FailingNutritionAuditStore(new NutritionCaseStore(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Cases(db, store).ExecuteAsync(new(NutritionOperation.Generate,
            patient.Id, null, null, NutritionAdmin, CompleteNutritionInput), CancellationToken.None));
        await using var verify = Context();
        Assert.Equal(0, await verify.NutritionPrescriptions.CountAsync());
        Assert.Equal(0, await verify.NutritionPrescriptionMeals.CountAsync());
        Assert.Equal(0, await verify.NutritionPrescriptionItems.CountAsync());
        Assert.Equal(0, await verify.NutritionPrescriptionEvents.CountAsync());
    }

    private sealed class FailingNutritionAuditStore(INutritionCaseStore inner) : INutritionCaseStore
    {
        public Task<NutritionCaseContext?> GetContextAsync(Guid id, CancellationToken ct) => inner.GetContextAsync(id, ct);
        public Task<bool> CanAccessAsync(Guid id, NutritionActor actor, CancellationToken ct) => inner.CanAccessAsync(id, actor, ct);
        public Task<T> ExecuteAsync<T>(Guid id, Func<CancellationToken, Task<T>> operation, CancellationToken ct) => inner.ExecuteAsync(id, operation, ct);
        public Task<int> NextVersionAsync(Guid id, CancellationToken ct) => inner.NextVersionAsync(id, ct);
        public Task<IReadOnlyList<NutritionPrescription>> PublishedAsync(Guid id, CancellationToken ct) => inner.PublishedAsync(id, ct);
        public Task AddEventAsync(NutritionPrescriptionEvent entry, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría inyectado");
        public Task<IReadOnlyList<NutritionPrescriptionEvent>> HistoryAsync(Guid id, CancellationToken ct) => inner.HistoryAsync(id, ct);
    }
}
