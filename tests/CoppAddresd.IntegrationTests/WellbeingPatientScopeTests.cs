using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task WellbeingGuard_RealSqlFiltersInboxCountsLegacyListsAndEnrollments_AndResolvesForeignResources()
    {
        await using var db = Context(); var own = await PatientAsync(db); var foreign = await PatientAsync(db);
        var professional = NutritionProfessional(own.Id); db.Professionals.Add(professional);
        db.PatientProfessionalAssignments.Add(new() { PatientId = own.Id, ProfessionalId = professional.Id });
        var a = InboxPlan(own.Id, InboxNow); var b = InboxPlan(foreign.Id, InboxNow);
        db.NutritionPrescriptions.AddRange(a, b);
        var analysis = new FoodAnalysis { AnalysisId = Guid.NewGuid(), UserId = foreign.UserId,
            DetectorVersion = "qa", SegmenterVersion = "qa", ClassifierVersion = "qa", PortionMethod = "qa" };
        db.FoodAnalyses.Add(analysis);
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Programa QA", TotalWeeks = 12 };
        db.ProgramTemplates.Add(template);
        var enrollments = new[] { own, foreign }.Select(p => new ProgramEnrollment
        { Id = Guid.NewGuid(), PatientId = p.Id, TemplateId = template.Id, Timezone = "UTC", StartLocalDate = new(2026, 11, 13), StartedAt = DateTime.UtcNow }).ToArray();
        db.ProgramEnrollments.AddRange(enrollments); await db.SaveChangesAsync();
        var actor = Substitute.For<IPatientScopeActor>(); actor.UserId.Returns(Guid.NewGuid());
        actor.ProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(professional.Id);
        actor.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var guard = new PatientScopeGuard(actor, new PatientScopeStore(db));
        // Incluso un ViewAll recibido de una capa anterior no reemplaza la identidad del guard.
        var read = new NutritionActor(Guid.NewGuid(), null, true, false, false, CanView: true);
        var inbox = new NutritionInboxRepository(db, guard);
        var page = await inbox.ListAsync(read, 1, 1, null, null, InboxNow, CancellationToken.None);
        Assert.Equal(1, page.Total); Assert.Equal(own.Id, Assert.Single(page.Items).PatientId);
        var empty = await inbox.ListAsync(read, 2, 1, null, null, InboxNow, CancellationToken.None);
        Assert.Empty(empty.Items); Assert.Equal(1, empty.Total);
        await guard.EnsureResourceAsync(PatientResource.Cycle, a.CycleId, "Nutrition.View", CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureResourceAsync(PatientResource.Cycle, b.CycleId, "Nutrition.View", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureResourceAsync(PatientResource.FoodAnalysis, analysis.AnalysisId, "Nutrition.RecordsView", CancellationToken.None));
        var programs = new ProgramRepository(db, new ConfigurationBuilder().Build(), patientScope: guard);
        var list = await programs.ListEnrollmentsAsync(null, null, 1, 20, ct: CancellationToken.None);
        Assert.Equal(1, list.Total); Assert.Equal(own.Id, Assert.Single(list.Items).PatientId);
        var ownLegacy = await DraftAsync(db, own.Id); var foreignLegacy = await DraftAsync(db, foreign.Id);
        await guard.EnsureNutritionPlansAsync([ownLegacy.Id], CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureNutritionPlansAsync([ownLegacy.Id, foreignLegacy.Id], CancellationToken.None));
        var plans = await new WellnessRepository(db, guard).ListPlansAsync(null, null, null, null, 1, 20, ct: CancellationToken.None);
        Assert.Equal(1, plans.Total); Assert.Equal(own.Id, Assert.Single(plans.Items).PatientId);
        actor.IsAdmin.Returns(true);
        var all = await inbox.ListAsync(read, 1, 20, null, null, InboxNow, CancellationToken.None);
        Assert.Equal(2, all.Total);
        await guard.EnsureResourceAsync(PatientResource.FoodAnalysis, analysis.AnalysisId, "Nutrition.RecordsView", CancellationToken.None);
    }
}
