using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Registry de estrategias de scoring (SPEC A9). Resuelve la implementación de
/// una estrategia por su código desde el DI. Agregar una estrategia nueva =
/// registrar la clase aquí y en el DI; nunca una migración.
/// </summary>
public sealed class ScoreStrategyRegistry(IServiceProvider services)
{
    private readonly Dictionary<HealthTestScoringStrategy, IScoreStrategy> _cache = [];

    public IScoreStrategy Resolve(HealthTestScoringStrategy strategy)
    {
        if (_cache.TryGetValue(strategy, out var cached))
        {
            return cached;
        }

        var instance =
            services.GetRequiredKeyedService<IScoreStrategy>(strategy)
            ?? throw new InvalidOperationException(
                $"Estrategia de scoring no registrada: {strategy}"
            );
        _cache[strategy] = instance;
        return instance;
    }
}
