using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Engine;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionEngineParityTests
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static IEnumerable<object[]> Cases() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Nutrition/Fixtures/engine-parity.json")))
        .RootElement.EnumerateArray().Select(x => new object[] { x.GetProperty("id").GetString()!, x.GetRawText() });

    [Theory]
    [MemberData(nameof(Cases))]
    public void TargetsAndWeight_MatchSevenPrototypeVectors(string id, string data)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        using var doc = JsonDocument.Parse(data); var row = doc.RootElement;
        var patient = row.GetProperty("input").Deserialize<NutritionPatient>(Json)!;
        Assert.Equal(row.GetProperty("targets").Deserialize<NutritionTargets>(Json), NutritionEngine.ComputeTargets(patient));
        Assert.Equal(row.GetProperty("weight").Deserialize<NutritionWeight>(Json), NutritionEngine.WeightPlan(patient));
    }

    [Theory]
    [InlineData(.5, 1)] [InlineData(-.5, 0)] [InlineData(2.5, 3)] [InlineData(-2.5, -2)]
    [InlineData(.49999999999999994, 0)]
    public void Round_UsesJavaScriptSemantics(double value, double expected) => Assert.Equal(expected, NutritionEngine.R0(value));

    [Fact]
    public void ZeroDeficitIsExplicit_WhileZeroKcalUsesAutomaticCalculation()
    {
        var p = new NutritionPatient(100, 170, 40, "M", 35, Mov: "MARCHA", Deficit: 0, Kcal: 0);
        Assert.Equal(0, NutritionEngine.ComputeTargets(p).Deficit);
        Assert.True(NutritionEngine.ComputeTargets(p).K >= 1500);
        Assert.Equal(1201, NutritionEngine.ComputeTargets(p with { Kcal = 1201 }).K);
    }
}
