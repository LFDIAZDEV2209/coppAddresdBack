using CoppAddresd.Application.Features.HealthTests.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cola en memoria para eventos de métricas de Tests de Salud (Channel pattern).
/// Desacopla la transacción de evaluación clínica del cálculo de analíticas.
/// </summary>
public interface IHealthTestMetricsQueue
{
    ValueTask EnqueueAsync(IHealthTestMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IHealthTestMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
