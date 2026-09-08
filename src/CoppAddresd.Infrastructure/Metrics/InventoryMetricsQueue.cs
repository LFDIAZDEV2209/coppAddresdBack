using System.Threading.Channels;
using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Cola en memoria (Channel delimitado, un solo lector) para eventos de
/// métricas de Inventario. El enqueue es no bloqueante: la latencia de
/// escritura HTTP no se ve afectada por la pre-agregación. Bajo sobrecarga
/// se descartan los eventos más antiguos (DropOldest) — las métricas son
/// aditivas y esa es la semántica aceptada (mismo precedente que
/// PatientMetricsQueue y HealthTestMetricsQueue).
/// </summary>
public sealed class InventoryMetricsQueue : IInventoryMetricsQueue
{
    private readonly Channel<IInventoryMetricEvent> _channel = Channel.CreateBounded<IInventoryMetricEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ValueTask EnqueueAsync(IInventoryMetricEvent metricEvent, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(metricEvent);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<IInventoryMetricEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}