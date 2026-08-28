using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Resultado de la clasificación de un valor contra los rangos de una versión.
/// </summary>
public sealed record RangeClassification(string Label, HealthTestSeverity Severity);

/// <summary>
/// Motor de interpretación: traduce un valor numérico a etiqueta + severidad
/// usando los rangos de la versión del instrumento (SPEC A9). Clasificación
/// por rangos [min, max] inclusive; si no hay rango coincidente, se clasifica
/// como severidad <c>low</c> con la etiqueta del rango más cercano o "sin clasificar".
/// </summary>
public sealed class ScoreRangeEngine
{
    public RangeClassification Classify(decimal value, IReadOnlyList<HealthTestScoreRange> ranges)
    {
        var active = ranges.Where(r => r.IsActive).OrderBy(r => r.MinValue).ToList();

        var match = active.FirstOrDefault(r => value >= r.MinValue && value <= r.MaxValue);
        if (match is not null)
        {
            return new RangeClassification(match.Label, match.Severity);
        }

        // Sin coincidencia exacta: clasifica con el rango más cercano (por
        // encima o por debajo), para no dejar un valor sin interpretación.
        var closest = active
            .OrderBy(r => Math.Min(Math.Abs(value - r.MinValue), Math.Abs(value - r.MaxValue)))
            .FirstOrDefault();
        if (closest is not null)
        {
            return new RangeClassification(closest.Label, closest.Severity);
        }

        return new RangeClassification("sin clasificar", HealthTestSeverity.low);
    }
}
