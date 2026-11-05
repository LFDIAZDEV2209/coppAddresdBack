using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionMealSolverTests
{
    internal static Dictionary<string, NutritionFoodItem> Foods() => JsonSerializer.Deserialize<NutritionFoodItem[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Nutrition/Fixtures/foods.json")), NutritionEngineParityTests.Json)!.ToDictionary(x => x.Id);

    [Fact]
    public void FullMonth_MatchesPrototypeFirstDay_AndAllThirtyDaysStayInBands()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Nutrition/Fixtures/month-parity.json")));
        var source = doc.RootElement; var p = source.GetProperty("input").Deserialize<NutritionPatient>(NutritionEngineParityTests.Json)!;
        var engine = new NutritionEngine(Foods()); var targets = NutritionEngine.ComputeTargets(p); var days = engine.BuildDays(p);
        Assert.Equal(30, days.Length);
        foreach (var key in NutritionEngine.MealOrder)
        {
            var expected = source.GetProperty("day1").GetProperty(key).EnumerateArray()
                .Select(i => new NutritionItem(i.GetProperty("id").GetString()!, i.GetProperty("g").GetDouble())).ToArray();
            Assert.Equal(expected, days[0].Meals[key]);
        }
        Assert.Equal(source.GetProperty("totalsDay1").Deserialize<NutritionMacros>(NutritionEngineParityTests.Json), engine.DayTotals(days[0]));
        Assert.All(days, d =>
        {
            Assert.Equal(4, d.Meals.Count); var total = engine.DayTotals(d);
            Assert.InRange(Math.Abs(total.K - targets.K) / targets.K, 0, .08);
            Assert.InRange(Math.Abs(total.C - targets.C) / targets.C, 0, .20);
            Assert.InRange(Math.Abs(total.F - targets.F) / targets.F, 0, .25);
            Assert.True(total.P >= targets.P * .9); Assert.True(total.Fi >= targets.Fi * .9);
        });
    }

    [Theory]
    [InlineData("A0", 2)] [InlineData("A1", 2)] [InlineData("A2", 4)] [InlineData("A3", 4)]
    public void Adherence_SelectsExactRotationAndOffsets(string adherence, int variety)
    {
        var p = new NutritionPatient(68.5, 161, 38, "F", 26.4, 5.9, "MARCHA", "perder", Adh: adherence);
        var engine = new NutritionEngine(Foods()); var days = engine.BuildDays(p);
        int[] offset = [0, 2, 1, 3];
        foreach (var day in days)
        for (var m = 0; m < 4; m++) Assert.Equal((day.D - 1 + offset[m]) % variety, day.Tpl[NutritionEngine.MealOrder[m]]);
        var first = days[0].Meals["des"]; days[variety].Meals["des"][0] = first[0] with { G = 999 };
        Assert.NotEqual(999, first[0].G);
    }

    [Theory]
    [InlineData("clara", 1, 66)] [InlineData("clara", 300, 198)]
    [InlineData("aceite", 1, 4.5)] [InlineData("aceite", 100, 13.5)]
    [InlineData("manzana", 1000, 270)] [InlineData("avena", 1, 30)]
    [InlineData("te", 28, 30)]
    public void Grams_UsesUnitStepsAndQuantizedCaps(string food, double grams, double expected)
        => Assert.Equal(expected, new NutritionEngine(Foods()).RoundGrams(food, grams));

    [Fact]
    public void Sofia1350_AllThirtyDaysMatchOriginalSolverIncludingItsDeviations()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Nutrition/Fixtures/sofia-parity.json")));
        var row = doc.RootElement; var p = row.GetProperty("input").Deserialize<NutritionPatient>(NutritionEngineParityTests.Json)!;
        var engine = new NutritionEngine(Foods()); var target = NutritionEngine.ComputeTargets(p);
        Assert.Equal(1350, target.K); var days = engine.BuildDays(p);
        for (var d = 0; d < 30; d++)
        {
            foreach (var key in NutritionEngine.MealOrder)
                Assert.Equal(row.GetProperty("days")[d].GetProperty(key).EnumerateArray().Select(i =>
                    new NutritionItem(i.GetProperty("id").GetString()!, i.GetProperty("g").GetDouble())), days[d].Meals[key]);
            Assert.Equal(row.GetProperty("totals")[d].Deserialize<NutritionMacros>(NutritionEngineParityTests.Json), engine.DayTotals(days[d]));
        }
    }

    [Fact]
    public void CancelledBuild_StopsBeforeSolving()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new NutritionEngine(Foods()).BuildDays(
            new NutritionPatient(68.5, 161, 38, "F", 26.4), cts.Token));
    }
}
