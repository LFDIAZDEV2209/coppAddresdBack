using System.Text.Json;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.LabExam;

namespace CoppAddresd.UnitTests.LabExam;

public sealed class LabExamEvolutionBuilderTests
{
    private static readonly DateTime PreviousDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CurrentDate = PreviousDate.AddDays(7);

    [Theory]
    [InlineData("systolic_bp", 120, 60, "decrease")]
    [InlineData("diastolic_bp", 80, 35, "decrease")]
    [InlineData("glucose_fasting", 100, 45, "decrease")]
    [InlineData("heart_rate", 70, 180, "increase")]
    public void Build_CambioInclusoPotencialmentePeligroso_NoJuzgaBeneficioClinico(
        string code, int previous, int current, string change)
    {
        var result = Compare(code, previous, current);

        Assert.Equal(MetricDirection.Changed, result.Direction);
        Assert.Equal(change, result.Change);
        Assert.Equal(current - previous, result.Delta);
        Assert.Equal(PreviousDate, result.PreviousDate);
        Assert.Equal(CurrentDate, result.CurrentDate);
        Assert.Equal("professional", result.PreviousSource);
        Assert.Equal("ocr_unverified", result.CurrentSource);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("improved", json);
        Assert.DoesNotContain("worsened", json);
    }

    [Fact]
    public void Build_CambioPequeno_ConservaDiferenciaSinUmbralClinico()
    {
        var result = Compare("hba1c", 6m, 6.1m);

        Assert.Equal(MetricDirection.Changed, result.Direction);
        Assert.Equal("increase", result.Change);
        Assert.Equal(.1m, result.Delta);
    }

    [Fact]
    public void Build_ValorIgual_IndicaIgualdadAritmetica()
    {
        var result = Compare("systolic_bp", 60m, 60m);

        Assert.Equal(MetricDirection.Stable, result.Direction);
        Assert.Equal("unchanged", result.Change);
        Assert.Equal(0m, result.Delta);
    }

    [Theory]
    [InlineData(null, "mg/dL", true, true, false)]
    [InlineData("mg/dL", null, true, true, false)]
    [InlineData("mg/dL", "mmol/L", true, true, false)]
    [InlineData("mg/dL", "mg/dL", false, true, false)]
    [InlineData("mg/dL", "mg/dL", true, false, false)]
    [InlineData("mg/dL", "mg/dL", true, true, true)]
    public void Build_UnidadFechaFaltanteODistintaOSinOrden_NoCalculaEvolucion(
        string? previousUnit, string? currentUnit, bool previousHasDate, bool currentHasDate, bool reversedDates)
    {
        var previous = new LabExamMetricSnapshot("glucose_fasting", 100, previousUnit,
            previousHasDate ? PreviousDate : null, "patient");
        var current = new LabExamMetricSnapshot("glucose_fasting", 50, currentUnit,
            currentHasDate ? (reversedDates ? PreviousDate.AddDays(-1) : CurrentDate) : null, "ocr_unverified");

        var result = Assert.Single(LabExamEvolutionBuilder.Build([current],
            new Dictionary<string, LabExamMetricSnapshot> { [previous.MetricCode] = previous })).Value;

        Assert.Equal("not_comparable", result.Change);
        Assert.Equal(MetricDirection.Changed, result.Direction);
        Assert.Null(result.Delta);
        Assert.Equal(100m, result.PreviousValue);
        Assert.Equal(50m, result.CurrentValue);
        Assert.Equal(previousUnit, result.PreviousUnit);
        Assert.Equal(currentUnit, result.CurrentUnit);
        Assert.Equal(previous.ObservedAt, result.PreviousDate);
        Assert.Equal(current.ObservedAt, result.CurrentDate);
    }

    [Fact]
    public void Build_SinAnterior_ConservaFechaUnidadDesconocidasYNoAfirmaDelta()
    {
        var current = new LabExamMetricSnapshot("weight", 70, null, null, "ocr_unverified");

        var result = Assert.Single(LabExamEvolutionBuilder.Build([current],
            new Dictionary<string, LabExamMetricSnapshot>())).Value;

        Assert.Equal(MetricDirection.FirstRecord, result.Direction);
        Assert.Equal("first_record", result.Change);
        Assert.Null(result.PreviousValue);
        Assert.Null(result.Delta);
        Assert.Null(result.CurrentDate);
        Assert.Null(result.CurrentUnit);
        Assert.Equal("ocr_unverified", result.CurrentSource);
    }

    [Theory]
    [InlineData("improved", MetricDirection.Improved)]
    [InlineData("worsened", MetricDirection.Worsened)]
    public void ContratoHistorico_SePuedeLeerPeroSerializaDireccionNeutral(string value, MetricDirection direction)
    {
        var parsed = JsonSerializer.Deserialize<MetricEvolution>("{\"direction\":\"" + value + "\"}");

        Assert.NotNull(parsed);
        Assert.Equal(direction, parsed.Direction);
        Assert.Equal("changed", JsonDocument.Parse(JsonSerializer.Serialize(parsed))
            .RootElement.GetProperty("direction").GetString());
    }

    private static MetricEvolution Compare(string code, decimal previous, decimal current)
        => Assert.Single(LabExamEvolutionBuilder.Build(
            [new(code, current, "unit", CurrentDate, "ocr_unverified")],
            new Dictionary<string, LabExamMetricSnapshot>
            {
                [code] = new(code, previous, "unit", PreviousDate, "professional")
            })).Value;
}
