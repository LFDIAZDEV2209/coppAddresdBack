using System.Threading.Channels;

namespace CoppAddresd.Community.Metrics;

/// <summary>
/// Cola en memoria (Channel delimitado, un solo lector) para eventos de
/// métricas de Comunidad. El enqueue es no bloqueante: la latencia de la
/// escritura HTTP no se ve afectada por la pre-agregación. Bajo sobrecarga
/// se descartan los eventos más antiguos (DropOldest) — las métricas son
/// aditivas y esa es la semántica aceptada (mismo precedente que las colas
/// de métricas del backend).
/// </summary>
public sealed class CommunityMetricsQueue : ICommunityMetricsQueue
{
    private readonly Channel<ICommunityMetricEvent> _channel = Channel.CreateBounded<ICommunityMetricEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ValueTask EnqueueAsync(ICommunityMetricEvent metricEvent, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(metricEvent);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<ICommunityMetricEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}