using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionMeasuredProfileTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTime Observed = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly NutritionCaseContext Context = new(new(1988, 1, 1), "F", "America/Bogota", true, Guid.NewGuid());
    private static readonly ClinicalContextDto Clinical = new(new(38, "F", null), [], [], [], [], new(null, null, null));
    private static AssignmentProfileData Data(params AssignmentResult[] results) => new(Guid.NewGuid(), Context.BatteryId!.Value, "F", null, results,
        [new(Guid.NewGuid(), "peso", 68.5m, "kg", 20, 300, Observed), new(Guid.NewGuid(), "talla", 161, "cm", 90, 250, Observed)]);
    private static AssignmentResult Result(string test, string code, string qualifier, string type = "score")
        => new(Guid.NewGuid(), test, type, code, 2, null, qualifier, 100, Observed);

    [Theory]
    [InlineData("bajo", "DESPERTAR", "prioritario", "A3", 1d, "bajo")]
    [InlineData("moderado", "FLUJO", "fragil", "A2", 2d, "moderado")]
    [InlineData("alto", "MARCHA", "consolidado", "A1", 3d, "elevado")]
    public void PublishedCategories_MapWithoutInventingScores(string label, string movement, string sleep, string adherence, double risk, string stress)
    {
        var data = Data(Result("movimiento", "total", label), Result("sueno", "total", label),
            Result("iac-adresd", "total", label), Result("orp", "total", label), Result("ers", "total", label),
            Result("nutricional", "Conducta emocional", label, "subscale"), Result("sueno", "iapnea", label, "indicator"));
        var profile = NutritionMeasuredProfiles.Resolve(Context, Clinical, data, [], Today);
        var patient = Assert.IsType<NutritionPatient>(profile.Patient);
        Assert.True(profile.CanGenerateWithoutManual); Assert.True(profile.HasMeasuredProfile);
        Assert.Equal(movement, patient.Mov); Assert.Equal(sleep, patient.SleepNivel); Assert.Equal(adherence, patient.Adh);
        Assert.Equal(risk, patient.RiesgoNum); Assert.Equal(stress, patient.Estres); Assert.Equal(label, patient.NutRiesgo);
        Assert.Equal(2, patient.SleepRF); Assert.Equal("health_test_result", profile.Fields["stress"].Source);
        Assert.Equal(Observed, profile.Fields["stress"].ObservedAt);
    }

    [Fact]
    public void MeasuredProfile_WinsOverManual_AndGlucoseFatRequireRealClinicalMeasurement()
    {
        var context = Clinical with { Measurements = [new("glucose", 100, "mg/dl", Observed), new("body_fat", 25, "%", Observed)] };
        var manual = new NutritionGenerateInput(new(200, 190, 25, "M", 40), new(2000, 1, 1));
        var absent = NutritionMeasuredProfiles.Resolve(Context, context, Data(), [], Today, manual);
        Assert.Equal(68.5, absent.Patient!.Peso); Assert.Equal("F", absent.Patient.Sexo); Assert.Equal(38, absent.Patient.Edad);
        Assert.Null(absent.Fields["glucose"].Value); Assert.Null(absent.Fields["bodyFatPercent"].Value);
        var measured = NutritionMeasuredProfiles.Resolve(Context, context, Data(),
            [new("glucose", 103, "mg/dL", Observed), new("body_fat", 26, "%", Observed)], Today);
        Assert.Equal(103m, measured.Fields["glucose"].Value); Assert.Equal(26m, measured.Fields["bodyFatPercent"].Value);
        Assert.Equal("clinical_measurements", measured.Fields["glucose"].Source);
    }

    [Fact]
    public void MissingOrUnclassifiedValues_RemainExplicitWithoutHealthyDefaults()
    {
        var data = Data(Result("nutricional", "Conducta sin clasificación", "", "subscale"));
        var profile = NutritionMeasuredProfiles.Resolve(Context, Clinical, data, [], Today);
        Assert.Null(profile.Patient!.NutRiesgo); Assert.Null(profile.Patient.SleepRF); Assert.Null(profile.Patient.Adh);
        Assert.Contains("nutritionRisk", profile.MissingData); Assert.Contains("sleepRiskFlags", profile.MissingData);
        Assert.Single(profile.Results); Assert.Null(profile.Fields["bodyFatPercent"].Value);
    }

    [Fact]
    public void NoProfile_RequiresMinimumFields_AndOnlyExplicitManualCanSupplyThem()
    {
        var context = Context with { BirthDate = null, Sex = null, TestsComplete = false, BatteryId = null };
        var empty = NutritionMeasuredProfiles.Resolve(context, Clinical, null, [], Today);
        Assert.Null(empty.Patient); Assert.False(empty.HasMeasuredProfile); Assert.False(empty.CanGenerateWithoutManual);
        Assert.Contains("weightKg", empty.MissingData);
        var manual = NutritionMeasuredProfiles.Resolve(context, Clinical, null, [], Today,
            new(new(70, 170, 30, "M", 24), new(1996, 1, 1)));
        Assert.NotNull(manual.Patient); Assert.False(manual.HasMeasuredProfile); Assert.False(manual.CanGenerateWithoutManual);
        Assert.Contains("bateria_9_tests", manual.MissingData);
    }
}
