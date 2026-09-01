using System.Text.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>Configuración de un indicador (jsonb <c>computation</c> de HealthTestIndicatorDef).</summary>
public sealed record IndicatorComputation
{
    /// <summary>Fórmula: <c>weighted</c>, <c>sum</c> o <c>avg</c>.</summary>
    [JsonPropertyName("formula")]
    public string Formula { get; init; } = "weighted";

    /// <summary>Fuentes: resultados de la evaluación (score/subscale/indicator).</summary>
    [JsonPropertyName("sources")]
    public IReadOnlyList<IndicatorSource> Sources { get; init; } = [];
}

/// <summary>Fuente de un indicador: un resultado de la evaluación.</summary>
public sealed record IndicatorSource
{
    [JsonPropertyName("resultType")]
    public string ResultType { get; init; } = "subscale";

    [JsonPropertyName("code")]
    public string Code { get; init; } = default!;

    [JsonPropertyName("weight")]
    public decimal? Weight { get; init; }
}

/// <summary>
/// Motor de indicadores derivados (SPEC A11). Lee los resultados de una
/// evaluación, aplica la fórmula configurada del indicador y produce el valor.
/// Soporta: <c>sum</c> (suma de fuentes), <c>avg</c> (promedio simple) y
/// <c>weighted</c> (promedio ponderado 0-100 si hay pesos, o promedio simple).
/// Puro y testeable en memoria.
/// </summary>
public sealed class IndicatorEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IndicatorComputation Parse(string computationJson)
    {
        var computation = JsonSerializer.Deserialize<IndicatorComputation>(
            computationJson,
            JsonOptions
        );
        return computation ?? new IndicatorComputation();
    }

    public decimal? Calculate(
        IndicatorComputation computation,
        IReadOnlyList<HealthTestResult> results
    )
    {
        // Resuelve las fuentes desde los resultados de la evaluación.
        var sources = new List<decimal>();
        foreach (var source in computation.Sources)
        {
            var match = results.FirstOrDefault(r =>
                r.Code == source.Code && r.ResultType.ToString() == source.ResultType
            );
            if (match is not null)
            {
                sources.Add(match.Value);
            }
        }

        if (sources.Count == 0)
        {
            return null;
        }

        return computation.Formula switch
        {
            "sum" => Math.Round(sources.Sum(), 2),
            "avg" => Math.Round(sources.Average(), 2),
            _ => WeightedAverage(computation.Sources, results),
        };
    }

    private static decimal? WeightedAverage(
        IReadOnlyList<IndicatorSource> sources,
        IReadOnlyList<HealthTestResult> results
    )
    {
        var resolved = new List<(decimal Value, decimal Weight)>();
        var hasWeights = sources.Any(s => s.Weight.HasValue);

        foreach (var source in sources)
        {
            var match = results.FirstOrDefault(r =>
                r.Code == source.Code && r.ResultType.ToString() == source.ResultType
            );
            if (match is null)
            {
                continue;
            }

            var weight = hasWeights ? (source.Weight ?? 1m) : 1m;
            resolved.Add((match.Value, weight));
        }

        if (resolved.Count == 0)
        {
            return null;
        }

        var totalWeight = resolved.Sum(x => x.Weight);
        if (totalWeight <= 0)
        {
            return null;
        }

        return Math.Round(resolved.Sum(x => x.Value * x.Weight) / totalWeight, 2);
    }
}
