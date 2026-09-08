using CoppAddresd.Application.Features.Inventory.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cola en memoria (Channel) para eventos de métricas de Inventario.
/// El enqueue NO bloquea al usuario; un background worker drena el canal.
/// </summary>
public interface IInventoryMetricsQueue
{
    ValueTask EnqueueAsync(IInventoryMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IInventoryMetricEvent> ReadAllAsync(CancellationToken ct = default);
}