using CoppAddresd.Application.Features.ProgramProgress.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cola en memoria no bloqueante para eventos de métricas biométricas (Channel pattern).
/// Desacopla la transacción de registro de mediciones del procesamiento analítico en segundo plano.
/// </summary>
public interface IBiometriaMetricsQueue
{
    ValueTask EnqueueAsync(IBiometriaMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IBiometriaMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
