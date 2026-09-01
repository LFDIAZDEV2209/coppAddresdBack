using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Xunit;

namespace CoppAddresd.UnitTests.Features.HealthTests;

/// <summary>
/// Pruebas del motor de rangos (SPEC A9): clasificación de un score contra los
/// rangos de la versión, incluidos los casos límite (mín/máx) y la fallback.
/// </summary>
public sealed class ScoreRangeEngineTests
{
    private static readonly Guid VersionId = Guid.NewGuid();

    private static readonly IReadOnlyList<HealthTestScoreRange> Ranges =
    [
        new HealthTestScoreRange
        {
            VersionId = VersionId,
            MinValue = 0,
            MaxValue = 25,
            Label = "bajo",
            Severity = HealthTestSeverity.low,
            IsActive = true,
        },
        new HealthTestScoreRange
        {
            VersionId = VersionId,
            MinValue = 26,
            MaxValue = 50,
            Label = "moderado",
            Severity = HealthTestSeverity.moderate,
            IsActive = true,
        },
        new HealthTestScoreRange
        {
            VersionId = VersionId,
            MinValue = 51,
            MaxValue = 75,
            Label = "alto",
            Severity = HealthTestSeverity.high,
            IsActive = true,
        },
        new HealthTestScoreRange
        {
            VersionId = VersionId,
            MinValue = 76,
            MaxValue = 100,
            Label = "critico",
            Severity = HealthTestSeverity.critical,
            IsActive = true,
        },
    ];

    private readonly ScoreRangeEngine _engine = new();

    [Theory]
    [InlineData(0, "bajo", "low")]
    [InlineData(25, "bajo", "low")]
    [InlineData(26, "moderado", "moderate")]
    [InlineData(50, "moderado", "moderate")]
    [InlineData(75, "alto", "high")]
    [InlineData(76, "critico", "critical")]
    [InlineData(100, "critico", "critical")]
    public void Classify_ValorEnRango_DevuelveEtiquetaYSeveridad(
        decimal value,
        string label,
        string severity
    )
    {
        var result = _engine.Classify(value, Ranges);

        Assert.Equal(label, result.Label);
        Assert.Equal(severity, result.Severity.ToString());
    }

    [Fact]
    public void Classify_ValorFueraDeRango_ClasificaConElMasCercano()
    {
        // 101 por encima del máximo: clasifica con el rango más cercano.
        var result = _engine.Classify(101m, Ranges);

        Assert.Equal("critico", result.Label);
        Assert.Equal(HealthTestSeverity.critical, result.Severity);
    }

    [Fact]
    public void Classify_RangoDesactivado_NoSeConsidera()
    {
        var ranges = new List<HealthTestScoreRange>
        {
            new()
            {
                VersionId = VersionId,
                MinValue = 0,
                MaxValue = 100,
                Label = "bajo",
                Severity = HealthTestSeverity.low,
                IsActive = true,
            },
            new()
            {
                VersionId = VersionId,
                MinValue = 0,
                MaxValue = 100,
                Label = "inactivo",
                Severity = HealthTestSeverity.critical,
                IsActive = false,
            },
        };

        var result = _engine.Classify(50m, ranges);

        Assert.Equal("bajo", result.Label);
        Assert.Equal(HealthTestSeverity.low, result.Severity);
    }

    [Fact]
    public void Classify_SinRangos_DevuelveFallback()
    {
        var result = _engine.Classify(50m, []);

        Assert.Equal("sin clasificar", result.Label);
        Assert.Equal(HealthTestSeverity.low, result.Severity);
    }
}
