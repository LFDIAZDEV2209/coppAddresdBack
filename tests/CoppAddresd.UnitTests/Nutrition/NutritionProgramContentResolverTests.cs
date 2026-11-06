using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionProgramContentResolverTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private readonly IWellnessRepository wellness = Substitute.For<IWellnessRepository>();
    private readonly INutritionPrescriptionRepository prescriptions = Substitute.For<INutritionPrescriptionRepository>();
    private ProgramContentResolver Resolver => new(wellness, NullLogger<ProgramContentResolver>.Instance, prescriptions);

    private static NutritionPrescription Published(Guid patient, DateOnly start) => new()
    {
        PatientId = patient, StartDate = start, EndDate = start.AddDays(29), State = "aprobado", IsPublished = true,
    };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Prescription_IsPrimary_WithOrWithoutWellness_AndRetainsExercise(bool withWellness)
    {
        var patient = Guid.NewGuid(); var plan = Published(patient, Today.AddDays(-29)); var routine = Guid.NewGuid();
        prescriptions.GetCurrentPublishedAsync(patient, Today, Arg.Any<CancellationToken>()).Returns(plan);
        wellness.ListAssignmentsByPatientAsync(patient, Arg.Any<CancellationToken>()).Returns([
            new RoutineAssignment { PatientId=patient, RoutineId=routine, StartDate=Today.ToDateTime(TimeOnly.MinValue), Status=AssignmentStatus.Active }]);
        if (withWellness) wellness.ListPlanAssignmentsByPatientAsync(patient,Arg.Any<CancellationToken>()).Returns([
            new NutritionPlanAssignment { PatientId=patient, PlanId=Guid.NewGuid(), StartDate=Today.ToDateTime(TimeOnly.MinValue), Status=AssignmentStatus.Active }]);
        using var cancel = new CancellationTokenSource();
        var result = await Resolver.ResolveAsync(patient, Today, cancel.Token);
        Assert.NotNull(result); Assert.Equal(plan.Id,result.SourcePrescriptionId); Assert.Equal(30,result.PrescriptionDayNumber);
        Assert.Null(result.NutritionPlanId); Assert.Null(result.NutritionPlanDayNumber); Assert.True(result.HasDayForWeekday);
        Assert.Equal(routine,result.ExerciseRoutineId);
        await wellness.DidNotReceive().GetPlanByIdAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>());
        await prescriptions.Received(1).GetCurrentPublishedAsync(patient,Today,cancel.Token);
    }

    [Fact]
    public async Task Range_ResetsThirtyToOneAtLocalMidnight_LoadsHeadersOnce_AndMatchesSingleDate()
    {
        var patient=Guid.NewGuid(); var c1=Published(patient,Today.AddDays(-29)); var c2=Published(patient,Today.AddDays(1));
        var draft=Published(patient,Today); draft.IsPublished=false; draft.State="borrador_ia";
        var invalid=Published(patient,Today.AddDays(-31)); invalid.EndDate=Today.AddDays(2);
        prescriptions.ListByPatientAsync(patient,1,100,Arg.Any<CancellationToken>()).Returns([draft,c2,c1,invalid]);
        var before=NutritionStateMachine.Today(DateTimeOffset.Parse("2026-11-06T04:59:59Z"),"America/Bogota");
        var after=NutritionStateMachine.Today(DateTimeOffset.Parse("2026-11-06T05:00:00Z"),"America/Bogota");
        var result=await Resolver.ResolveRangeAsync(patient,before,after.AddDays(30));
        Assert.Equal(c1.Id,result[before].SourcePrescriptionId); Assert.Equal(30,result[before].PrescriptionDayNumber);
        Assert.Equal(c2.Id,result[after].SourcePrescriptionId); Assert.Equal(1,result[after].PrescriptionDayNumber);
        Assert.False(result.ContainsKey(after.AddDays(30)));
        await prescriptions.Received(1).ListByPatientAsync(patient,1,100,Arg.Any<CancellationToken>());
        Assert.All(result.Values,r=> { Assert.Null(r.NutritionPlanId); Assert.Null(r.NutritionPlanDayNumber); });
        prescriptions.GetCurrentPublishedAsync(patient,after,Arg.Any<CancellationToken>()).Returns(c2);
        Assert.Equal(result[after],await Resolver.ResolveAsync(patient,after));
    }

    [Fact]
    public async Task NoPrescription_FallsBackToPublishedWellness_ForSingleDateAndRange()
    {
        var plan=NutritionPlanReviewRulesTests.ValidPlan(); plan.Status=NutritionPlanStatus.Active;
        plan.ReviewStatus="approved"; plan.ApprovedBy=Guid.NewGuid(); plan.ApprovedAt=DateTime.UtcNow;
        var patient=plan.PatientId!.Value;
        wellness.ListPlanAssignmentsByPatientAsync(patient,Arg.Any<CancellationToken>()).Returns([
            new NutritionPlanAssignment { PatientId=patient,PlanId=plan.Id,StartDate=Today.ToDateTime(TimeOnly.MinValue),Status=AssignmentStatus.Active }]);
        wellness.GetPlanByIdAsync(plan.Id,Arg.Any<CancellationToken>()).Returns(plan);
        var single=await Resolver.ResolveAsync(patient,Today);
        Assert.NotNull(single); Assert.Equal(plan.Id,single.NutritionPlanId); Assert.Equal(1,single.NutritionPlanDayNumber);
        Assert.Null(single.SourcePrescriptionId); Assert.Null(single.PrescriptionDayNumber);
        var range=await Resolver.ResolveRangeAsync(patient,Today,Today.AddDays(2));
        Assert.Equal(single,range[Today]); Assert.Equal(3,range[Today.AddDays(2)].NutritionPlanDayNumber);
        await wellness.Received(2).GetPlanByIdAsync(plan.Id,Arg.Any<CancellationToken>());
    }
}
