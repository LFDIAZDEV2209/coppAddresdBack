using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Scores;

/// <summary>
/// Pruebas unitarias del calculador del Índice de Transformación (SPEC §13.5):
/// las 9 bandas de <c>|pct_change|</c> con dirección, el detalle <c>jsonb</c>
/// y el default sin indicadores (AC-20/AC-21).
/// </summary>
public sealed class TransformationScoreCalculatorTests
{
    private static readonly ScoreCalculationContext Context = new(
        Guid.NewGuid(), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), ScoreTrigger.OnRead);

    private readonly TransformationScoreCalculator _calculator =
        new(NullLogger<TransformationScoreCalculator>.Instance);

    private static TransformationIndicator Indicator(
        decimal baseline, decimal current,
        FavorableDirection direction = FavorableDirection.LowerIsBetter,
        string metric = "weight", string unit = "kg")
        => new(metric, baseline, current, unit, direction);

    private TransformationScoreResult Run(params TransformationIndicator[] indicators)
        => _calculator.Calculate(indicators, Context);

    /// <summary>
    /// AC-20: 3 métricas con |Δ%| = 18%, 7% y 12% (favorables) →
    /// round((100 + 75 + 90) / 3) = 88; el detalle expone delta/delta_pct.
    /// </summary>
    [Fact]
    public void Calcular_TresIndicadoresAC20_Devuelve88ConDetalle()
    {
        var result = Run(
            Indicator(100m, 118m, FavorableDirection.HigherIsBetter, "weight"),  // +18% → 100
            Indicator(100m, 107m, FavorableDirection.HigherIsBetter, "bmi"),     // +7%  → 75
            Indicator(100m, 112m, FavorableDirection.HigherIsBetter, "glucose"));// +12% → 90

        Assert.Equal(88, result.Score);
        Assert.Equal(3, result.Detail.Count);

        var weight = result.Detail["weight"];
        Assert.Equal(100m, weight.Baseline);
        Assert.Equal(118m, weight.Current);
        Assert.Equal("kg", weight.Unit);
        Assert.Equal(18m, weight.Delta);
        Assert.Equal(18m, weight.DeltaPct);
        Assert.True(weight.Favorable);
        Assert.Equal(100, weight.Score);

        Assert.Equal(75, result.Detail["bmi"].Score);
        Assert.Equal(90, result.Detail["glucose"].Score);
    }

    /// <summary>AC-20 (shape del detail): delta_pct redondeado a 2 decimales.</summary>
    [Fact]
    public void Calcular_DetailRedondeaDeltaPctA2Decimales()
    {
        var result = Run(Indicator(82.5m, 78.0m, FavorableDirection.LowerIsBetter, "weight", "kg"));

        var detail = result.Detail["weight"];
        Assert.Equal(-4.5m, detail.Delta);
        // (78.0 - 82.5) / 82.5 * 100 = -5.4545... → -5.45
        Assert.Equal(-5.45m, detail.DeltaPct);
        Assert.True(detail.Favorable);
        // |5.45%| con LowerIsBetter favorable → banda >= 5% → 75.
        Assert.Equal(75, detail.Score);
    }

    // ------------------------------------------------------------ bandas favorables

    [Theory]
    [InlineData(115, 100)] // >= 15% → 100
    [InlineData(110, 90)]  // >= 10% → 90
    [InlineData(105, 75)]  // >= 5%  → 75
    [InlineData(101, 60)]  // >= 1%  → 60
    [InlineData(100, 50)]  // < 1% estable → 50
    public void Calcular_BandasFavorables(decimal current, int expected)
        => Assert.Equal(expected, Run(Indicator(100m, current, FavorableDirection.HigherIsBetter)).Score);

    // ------------------------------------------------------------ bandas desfavorables

    [Theory]
    [InlineData(115, 10)] // >= 10% desfavorable → 10
    [InlineData(105, 25)] // >= 5% desfavorable → 25
    [InlineData(102, 35)] // >= 2% desfavorable → 35
    [InlineData(101, 45)] // [1%, 2%) desfavorable → 45
    public void Calcular_BandasDesfavorables(decimal current, int expected)
        // LowerIsBetter: subir es desfavorable (current > baseline).
        => Assert.Equal(expected, Run(Indicator(100m, current, FavorableDirection.LowerIsBetter)).Score);

    /// <summary>AC-21: sin indicadores → score 0 y detalle vacío (SPEC §13.5).</summary>
    [Fact]
    public void Calcular_SinIndicadores_Devuelve0ConDetalleVacio()
    {
        var result = Run();

        Assert.Equal(0, result.Score);
        Assert.Empty(result.Detail);
    }
}