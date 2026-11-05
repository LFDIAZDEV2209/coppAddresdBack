using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private static Professional NutritionProfessional(Guid patientId) => new()
    {
        Id = Guid.NewGuid(), Employee = new Employee { Id = Guid.NewGuid(), FirstName = "Nutricionista", LastName = "QA",
            Email = $"nutrition-{patientId:N}@example.invalid", Organization = new Organization
                { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Clínica QA" } }
    };

    [NutritionPostgresFact]
    public async Task Scope_NewModuleEnforcesOwnForeignGlobal_AndSeparateApprovalPermission()
    {
        await using var db = Context(); var own = await PatientAsync(db); var foreign = await PatientAsync(db);
        var professional = NutritionProfessional(own.Id); db.Professionals.Add(professional);
        db.PatientProfessionalAssignments.Add(new() { PatientId = own.Id, ProfessionalId = professional.Id }); await db.SaveChangesAsync();
        var actor = new NutritionActor(Guid.NewGuid(), professional.Id, false, true, false); var cases = Cases(db);
        var plan = await cases.ExecuteAsync(new(NutritionOperation.Generate, own.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => cases.ExecuteAsync(new(NutritionOperation.Generate, foreign.Id, null, null,
            actor, CompleteNutritionInput), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, actor), CancellationToken.None));
        var global = NutritionAdmin;
        var other = await cases.ExecuteAsync(new(NutritionOperation.Generate, foreign.Id, null, null, global, CompleteNutritionInput), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => cases.GetAsync(new(own.Id, other.Id, actor), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => cases.HistoryAsync(new(foreign.Id, other.Id, actor), CancellationToken.None));
        Assert.Equal(other.Id, (await cases.GetAsync(new(foreign.Id, other.Id, global), CancellationToken.None)).Id);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Submit, actor), CancellationToken.None);
        var reviewer = actor with { UserId = Guid.NewGuid(), CanManage = false, CanApprove = true };
        await Assert.ThrowsAsync<ForbiddenException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Save, reviewer), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => cases.ExecuteAsync(Mutation(plan, NutritionOperation.Return, actor, comment: "Sin permiso de revisión"), CancellationToken.None));
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Return, reviewer, comment: "Revisar antes de publicar"), CancellationToken.None);
        plan = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Submit, actor), CancellationToken.None);
        var approved = await cases.ExecuteAsync(Mutation(plan, NutritionOperation.Approve, reviewer, confirm: true), CancellationToken.None);
        Assert.Equal(reviewer.UserId, approved.ApprovedBy);
        Assert.False((await db.NutritionPrescriptionEvents.SingleAsync(e => e.Action == "approve")).AutoApproved);
    }

    [NutritionPostgresFact]
    public async Task Scope_RequestCancelledDuringDraft_PersistsNothing()
    {
        await using var db = Context(); var patient = await PatientAsync(db); using var cts = new CancellationTokenSource();
        var client = Substitute.For<INutritionDraftClient>();
        client.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            cts.Cancel();
            return new NutritionDraftResponse(new("ES", "EN"), new("ES", "EN"), call.Arg<NutritionDraftRequest>().Menus, "llm");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cases(db, client: client).ExecuteAsync(new(
            NutritionOperation.Generate, patient.Id, null, null, NutritionAdmin, CompleteNutritionInput), cts.Token));
        await using var verify = Context();
        Assert.Equal(0, await verify.NutritionPrescriptions.CountAsync()); Assert.Equal(0, await verify.NutritionPrescriptionEvents.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task Scope_RevokedWhileLlmRuns_IsCheckedAgainBeforePersisting()
    {
        await using var db = Context(); var own = await PatientAsync(db); var professional = NutritionProfessional(own.Id);
        db.Professionals.Add(professional);
        db.PatientProfessionalAssignments.Add(new() { PatientId = own.Id, ProfessionalId = professional.Id }); await db.SaveChangesAsync();
        var actor = new NutritionActor(Guid.NewGuid(), professional.Id, false, true, false);
        var client = Substitute.For<INutritionDraftClient>();
        client.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            await db.PatientProfessionalAssignments.Where(x => x.PatientId == own.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Inactive"));
            return new NutritionDraftResponse(new("ES", "EN"), new("ES", "EN"), call.Arg<NutritionDraftRequest>().Menus, "llm");
        });
        await Assert.ThrowsAsync<NotFoundException>(() => Cases(db, client: client).ExecuteAsync(new(NutritionOperation.Generate,
            own.Id, null, null, actor, CompleteNutritionInput), CancellationToken.None));
        await using var verify = Context();
        Assert.Equal(0, await verify.NutritionPrescriptions.CountAsync()); Assert.Equal(0, await verify.NutritionPrescriptionEvents.CountAsync());
    }
}
