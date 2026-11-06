using System.Text.Json;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Application.Features.HealthTests.Assignments;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionPlanReviewRulesTests
{
    internal static NutritionPlan ValidPlan() => new()
    {
        Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), IsTemplate = false, Name = "Menú revisable",
        DurationDays = 30, ReviewStatus = "pending_review", Status = NutritionPlanStatus.Draft,
        Days = Enumerable.Range(1, 7).SelectMany(day => new[] { MealType.Desayuno, MealType.Almuerzo, MealType.Snack, MealType.Cena }
            .Select(meal => new NutritionPlanDay { Id = Guid.NewGuid(), DayNumber = day, MealType = meal, Foods = "Alimentos del plan" })).ToList(),
    };

    [Fact]
    public void Approval_CompleteWeeklyMenuForThirtyDayCycle_Accepted() => NutritionPlanReviewRules.ValidateForApproval(ValidPlan());

    [Theory]
    [InlineData("queued")]
    [InlineData("generating")]
    [InlineData("failed")]
    public void Approval_UnfinishedGeneration_IsRejected(string status)
    {
        var plan = ValidPlan(); plan.GenerationStatus = status;
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanReviewRules.ValidateForApproval(plan));
    }

    [Fact]
    public void Approval_OnlyOneDayCannotPretendToBeACompleteWeeklyMenu()
    {
        var plan = ValidPlan(); plan.Days = plan.Days.Where(d => d.DayNumber == 1).ToList();
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanReviewRules.ValidateForApproval(plan));
    }

    [Fact]
    public void Approval_MissingSnack_IsRejected()
    {
        var plan = ValidPlan(); plan.Days = plan.Days.Where(d => d.MealType != MealType.Snack).ToList();
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanReviewRules.ValidateForApproval(plan));
    }

    [Fact]
    public void Approval_WithoutPatientOrTemplate_IsRejected()
    {
        var plan = ValidPlan(); plan.IsTemplate = true;
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanReviewRules.ValidateForApproval(plan));
        plan.IsTemplate = false; plan.PatientId = null;
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanReviewRules.ValidateForApproval(plan));
    }

    [Fact]
    public void Published_StatusActiveWithoutApproval_DoesNotExposeMenu()
    {
        var plan = ValidPlan(); plan.Status = NutritionPlanStatus.Active;
        Assert.False(NutritionPlanReviewRules.IsPublished(plan));
        Assert.Null(NutritionPlanReviewRules.MenuDay(plan, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 7)]
    [InlineData(7, 1)]
    [InlineData(29, 2)]
    [InlineData(30, null)]
    [InlineData(-1, null)]
    public void MenuDay_UsesElapsedCycleAndExpiresAfterThirtyDays(int elapsed, int? expected)
    {
        var plan = ValidPlan(); plan.Status = NutritionPlanStatus.Active; plan.ReviewStatus = "approved";
        plan.ApprovedAt = DateTime.UtcNow; plan.ApprovedBy = Guid.NewGuid();
        var start = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, NutritionPlanReviewRules.MenuDay(plan, start, DateOnly.FromDateTime(start).AddDays(elapsed)));
    }

    [Fact]
    public void InvalidAiPayload_DoesNotReplaceTheExistingDraft()
    {
        var plan = ValidPlan(); var name = plan.Name; var meals = plan.Days;
        var payload = JsonSerializer.SerializeToElement(new { name = "Respuesta incompleta", duration_days = 30, days = new[] { new { day_number = 1, meal_type = "Desayuno", foods = "Avena" } } });
        Assert.Throws<UnprocessableEntityException>(() => NutritionPlanWorkflow.ApplyGeneratedPlan(plan, payload));
        Assert.Equal(name, plan.Name); Assert.Same(meals, plan.Days);
    }

    [Fact]
    public async Task CompletedOrConcurrentlyCompletedAssessment_CannotBeCancelled()
    {
        var repository = Substitute.For<IHealthTestRepository>();
        var assignment = new HealthTestAssignment { Id = Guid.NewGuid(), Status = HealthTestAssignmentStatus.completed };
        repository.GetAssignmentByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        var handler = new CancelAssignmentCommandHandler(repository);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(new(assignment.Id), CancellationToken.None));
        assignment.Status = HealthTestAssignmentStatus.pending;
        repository.CancelAssignmentIfOpenAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(false);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(new(assignment.Id), CancellationToken.None));
        await repository.DidNotReceiveWithAnyArgs().UpdateAssignmentAsync(default!, default);
    }

    [Fact]
    public async Task TemplateWithForeignPatientId_DoesNotPersistOrReturnThatPatient()
    {
        var repository = Substitute.For<IWellnessRepository>(); NutritionPlan? saved = null;
        repository.AddPlanAsync(Arg.Any<NutritionPlan>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<NutritionPlan>(); return saved;
        });
        repository.GetPlanByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => saved);
        var request = new CreateNutritionPlanRequest("Plantilla", null, null, 30, null, null, null, null, null,
            null, null, true, Guid.NewGuid(), null, NutritionPlanStatus.Draft, null);
        var handler = new CreateNutritionPlanCommandHandler(repository, NullLogger<CreateNutritionPlanCommandHandler>.Instance);
        var result = await handler.Handle(new(request, Guid.NewGuid()), CancellationToken.None);
        Assert.Null(saved!.PatientId); Assert.Null(result.PatientId); Assert.Null(result.PatientName);
    }

    [Fact]
    public async Task ProgramResolver_UnapprovedAssignment_DoesNotRevealPlanOrFoods()
    {
        var repository = Substitute.For<IWellnessRepository>(); var plan = ValidPlan();
        repository.ListPlanAssignmentsByPatientAsync(plan.PatientId!.Value, Arg.Any<CancellationToken>()).Returns([
            new NutritionPlanAssignment { PatientId = plan.PatientId.Value, PlanId = plan.Id, StartDate = DateTime.UtcNow.AddDays(-1), Status = AssignmentStatus.Active }
        ]);
        repository.ListAssignmentsByPatientAsync(plan.PatientId.Value, Arg.Any<CancellationToken>()).Returns(Array.Empty<RoutineAssignment>());
        repository.GetPlanByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var resolver = new ProgramContentResolver(repository, NullLogger<ProgramContentResolver>.Instance, Substitute.For<INutritionPrescriptionRepository>());
        var result = await resolver.ResolveAsync(plan.PatientId.Value, DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        Assert.Null(result?.NutritionPlanId); Assert.Null(result?.NutritionPlanDayNumber);
    }
}
