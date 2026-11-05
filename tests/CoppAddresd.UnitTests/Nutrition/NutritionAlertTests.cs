using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Engine;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionAlertTests
{
    [Theory]
    [MemberData(nameof(NutritionEngineParityTests.Cases), MemberType = typeof(NutritionEngineParityTests))]
    public void ClinicalFlags_MatchExactPrototypeMessagesAndOrder(string id, string data)
    {
        Assert.NotEmpty(id); using var doc = JsonDocument.Parse(data); var row = doc.RootElement;
        var patient = row.GetProperty("input").Deserialize<NutritionPatient>(NutritionEngineParityTests.Json)!;
        Assert.Equal(row.GetProperty("flags").Deserialize<NutritionAlert[]>(NutritionEngineParityTests.Json),
            NutritionEngine.ClinicalFlags(patient, NutritionEngine.ComputeTargets(patient)));
    }

    [Theory]
    [InlineData("apnea", 1.99, false)] [InlineData("apnea", 2, true)]
    [InlineData("cardio", 3.99, false)] [InlineData("cardio", 4, true)]
    [InlineData("hba1c", 6.49, false)] [InlineData("hba1c", 6.5, true)]
    [InlineData("imc", 34.99, false)] [InlineData("imc", 35, true)]
    [InlineData("bajo", 18.5, false)] [InlineData("bajo", 18.49, true)]
    public void CriticalThresholds_RespectInclusiveAndExclusiveBoundaries(string rule, double value, bool critical)
    {
        var p = new NutritionPatient(65, 165, 40, "F", 24, Objetivo: "mantener");
        p = rule switch { "apnea" => p with { SleepRF = value }, "cardio" => p with { RiesgoNum = value },
            "hba1c" => p with { Hba1c = value }, _ => p with { Imc = value } };
        var flags = NutritionEngine.ClinicalFlags(p, NutritionEngine.ComputeTargets(p));
        Assert.Equal(critical, flags.Any(f => f.S == "crit"));
        Assert.Equal(!critical, NutritionEngine.CanApprove(flags, false));
        Assert.True(NutritionEngine.CanApprove(flags, true));
    }

    [Theory]
    [InlineData("alto", true)] [InlineData("moderado", false)] [InlineData(null, false)]
    public void EatingRisk_CriticalOnlyForHighRisk(string? risk, bool expected)
    {
        var p = new NutritionPatient(65, 165, 40, "F", 24, NutRiesgo: risk);
        Assert.Equal(expected, NutritionEngine.ClinicalFlags(p, NutritionEngine.ComputeTargets(p)).Any(f => f.S == "crit"));
    }

    [Fact]
    public void TechnicalRules_IdentifyEachDeviation_AndExcludeBulk()
    {
        var engine = new NutritionEngine(NutritionMealSolverTests.Foods());
        var t = NutritionEngine.ComputeTargets(new(68.5, 161, 38, "F", 26.4, 5.9, "MARCHA"));
        var days = engine.BuildDays(t);
        Assert.Empty(engine.ValidatePlan(t, days));
        var mismatched = t with { K = 1000, P = 1000, C = 1, F = 1, Fi = 1000 };
        var flags = engine.ValidatePlan(mismatched, days);
        Assert.Equal(6, flags.Count); Assert.All(flags, f => Assert.True(f.Tec));
        Assert.Equal("crit", flags[0].S); Assert.False(NutritionEngine.EligibleBulk(flags));
        Assert.True(NutritionEngine.EligibleBulk([new("info", "información")]));
        Assert.False(NutritionEngine.EligibleBulk([new("warn", "técnica", true)]));
    }
}
