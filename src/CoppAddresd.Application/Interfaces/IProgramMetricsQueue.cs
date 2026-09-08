using CoppAddresd.Application.Features.ProgramProgress.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Canal asíncrono en memoria para encolar eventos de métricas con 0ms de bloqueo en el hilo de escritura.
/// </summary>
public interface IProgramMetricsQueue
{
    ValueTask EnqueueAsync(IProgramMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IProgramMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
