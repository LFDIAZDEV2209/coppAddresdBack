using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionLegacyPreservationTests
{
    private readonly IWellnessRepository repository = Substitute.For<IWellnessRepository>();
    private static CreateNutritionPlanDayRequest Meal => new(1, MealType.Desayuno,
        "Desayuno", "Avena", 410, 22.5m, 45.2m, 12.1m, 8.3m, 350, "Revisado", 1, null, 2350);

    private void ReadSavedPlan()
    {
        repository.AddPlanAsync(Arg.Any<NutritionPlan>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<NutritionPlan>());
        repository.When(x => x.AddPlanAsync(Arg.Any<NutritionPlan>(), Arg.Any<CancellationToken>()))
            .Do(call => repository.GetPlanByIdAsync(call.Arg<NutritionPlan>().Id, Arg.Any<CancellationToken>())
                .Returns(call.Arg<NutritionPlan>()));
    }

    private static void AssertFields(NutritionPlanDto plan)
    {
        Assert.Equal(92.5m, plan.DailyProteinTarget); Assert.Equal(185.2m, plan.DailyCarbsTarget);
        Assert.Equal(55.1m, plan.DailyFatTarget); Assert.Equal(28.3m, plan.DailyFiberTarget);
        Assert.Equal("leche", plan.Allergens); Assert.Equal("07:00", plan.MealTiming);
        var meal = Assert.Single(plan.Days);
        Assert.Equal(Meal.ProteinG, meal.ProteinG); Assert.Equal(Meal.CarbsG, meal.CarbsG);
        Assert.Equal(Meal.FatG, meal.FatG); Assert.Equal(Meal.FiberG, meal.FiberG);
        Assert.Equal(Meal.WaterMl, meal.WaterMl); Assert.Equal(Meal.DailyWaterMl, meal.DailyWaterMl);
    }

    [Fact]
    public async Task Create_PreservesAllNutritionFields()
    {
        ReadSavedPlan();
        var handler = new CreateNutritionPlanCommandHandler(repository, NullLogger<CreateNutritionPlanCommandHandler>.Instance);
        var request = new CreateNutritionPlanRequest("Plan", null, null, 30, 1800,
            92.5m, 185.2m, 55.1m, 28.3m, "leche", "07:00", false, Guid.NewGuid(), null,
            NutritionPlanStatus.Draft, [Meal]);
        AssertFields(await handler.Handle(new(request), CancellationToken.None));
    }

    [Fact]
    public async Task Update_PreservesAllNutritionFields_AndUsesTransactionToken()
    {
        var plan = new NutritionPlan { Id = Guid.NewGuid(), Name = "Anterior", IsTemplate = false };
        repository.GetPlanByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        using var transactionCancellation = new CancellationTokenSource();
        repository.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<NutritionPlanDto>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<NutritionPlanDto>>>()(transactionCancellation.Token));
        var handler = new UpdateNutritionPlanCommandHandler(repository, NullLogger<UpdateNutritionPlanCommandHandler>.Instance);
        var request = new UpdateNutritionPlanRequest("Plan", null, null, 30, 1800,
            92.5m, 185.2m, 55.1m, 28.3m, "leche", "07:00", NutritionPlanStatus.Draft, [Meal], 1);
        AssertFields((await handler.Handle(new(plan.Id, request), CancellationToken.None))!);
        await repository.Received(1).DeletePlanDaysByPlanIdAsync(plan.Id, transactionCancellation.Token);
        await repository.Received(1).AddPlanDaysRangeAsync(Arg.Any<IEnumerable<NutritionPlanDay>>(), transactionCancellation.Token);
        await repository.Received(1).UpdatePlanAsync(plan, transactionCancellation.Token);
    }

    [Fact]
    public async Task Clone_PreservesAllNutritionFields_AndDoesNotMutateSource()
    {
        ReadSavedPlan();
        var source = new NutritionPlan { Id = Guid.NewGuid(), Name = "Plantilla", IsTemplate = true,
            DailyProteinTarget = 92.5m, DailyCarbsTarget = 185.2m, DailyFatTarget = 55.1m,
            DailyFiberTarget = 28.3m, Allergens = "leche", MealTiming = "07:00",
            Days = [new() { Id = Guid.NewGuid(), DayNumber = 1, MealType = Meal.MealType,
                ProteinG = Meal.ProteinG, CarbsG = Meal.CarbsG, FatG = Meal.FatG,
                FiberG = Meal.FiberG, WaterMl = Meal.WaterMl, DailyWaterMl = Meal.DailyWaterMl }] };
        repository.GetPlanByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        var handler = new CloneNutritionPlanCommandHandler(repository, NullLogger<CloneNutritionPlanCommandHandler>.Instance);
        var result = await handler.Handle(new(source.Id, Guid.NewGuid()), CancellationToken.None);
        AssertFields(result!);
        Assert.NotEqual(source.Id, result!.Id); Assert.NotEqual(source.Days.Single().Id, result.Days.Single().Id);
        Assert.True(source.IsTemplate);
    }
}
