using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.Scores;

/// <summary>
/// Pruebas del validador de la suma de pesos del Índice de Salud (AC-19,
/// SPEC §13.1.1): antes de cualquier escritura sobre
/// <c>app.health_score_weights</c> se exige <c>SUM(weight) = 1.0000</c>.
/// Defensa contra una mala configuración clínica (PLAN §7).
/// </summary>
public sealed class WeightSumValidatorTests
{
    private static readonly IReadOnlyList<ScoreWeight> DefaultWeights =
    [
        new(ScoreDimension.adherence, 0.30m),
        new(ScoreDimension.clinical, 0.30m),
        new(ScoreDimension.nutrition, 0.20m),
        new(ScoreDimension.psychology, 0.10m),
        new(ScoreDimension.exercise, 0.10m),
    ];

    /// <summary>Los 5 pesos por defecto del seeder suman exactamente 1.0000.</summary>
    [Fact]
    public void Validate_PesosPorDefecto_EsValido()
    {
        var result = WeightSumValidator.Validate(DefaultWeights);

        Assert.True(result.IsValid);
        Assert.Equal(1.0000m, result.Sum);
    }

    /// <summary>Una suma distinta de 1.0000 (p. ej. 1.0500) se rechaza.</summary>
    [Fact]
    public void Validate_SumaDiferenteDe1_EsInvalido()
    {
        var weights = DefaultWeights.Select(w => w with { Weight = w.Weight * 1.05m }).ToList();

        var result = WeightSumValidator.Validate(weights);

        Assert.False(result.IsValid);
        Assert.Equal(1.0500m, result.Sum);
    }

    /// <summary>Una lista vacía no puede ponderar nada: se rechaza.</summary>
    [Fact]
    public void Validate_SinPesos_EsInvalido()
    {
        var result = WeightSumValidator.Validate([]);

        Assert.False(result.IsValid);
        Assert.Equal(0m, result.Sum);
    }

    /// <summary>Pesos negativos se rechazan (invalida el rango 0..1 del CHECK).</summary>
    [Fact]
    public void Validate_PesoNegativo_EsInvalido()
    {
        var weights = DefaultWeights
            .Select(w => w with { Weight = w.Dimension == ScoreDimension.exercise ? -0.10m : w.Weight })
            .ToList();

        var result = WeightSumValidator.Validate(weights);

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Faltar una dimensión con la suma ajustada a 1.0000 pasa la validación
    /// de suma (el CHECK del schema exige el UNIQUE por dimensión; el
    /// ponderado del calculador normaliza y no pondera la dimensión ausente).
    /// </summary>
    [Fact]
    public void Validate_SinUnaDimensionPeroSuma1_EsValido()
    {
        // Sin exercise; psychology sube de 0.10 a 0.20 para que la suma sea
        // exactamente 1.0000 (0.30 + 0.30 + 0.20 + 0.20). Evita división
        // decimal que arrastraría 0.999...9 por precisión.
        var weights = DefaultWeights
            .Where(w => w.Dimension != ScoreDimension.exercise)
            .Select(w => w with { Weight = w.Dimension == ScoreDimension.psychology ? 0.20m : w.Weight })
            .ToList();

        var result = WeightSumValidator.Validate(weights);

        Assert.True(result.IsValid);
        Assert.Equal(1.0000m, result.Sum);
    }
}