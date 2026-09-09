using System.Threading.Channels;
using CoppAddresd.Application.Features.ProgramProgress.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Implementación de cola en memoria de alto rendimiento usando BoundedChannel.
/// Emite y consume eventos biométricos con cero contención de locks y sin impactar latencia HTTP.
/// </summary>
public sealed class BiometriaMetricsQueue : IBiometriaMetricsQueue
{
    private readonly Channel<IBiometriaMetricEvent> _channel = Channel.CreateBounded<IBiometriaMetricEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ValueTask EnqueueAsync(IBiometriaMetricEvent metricEvent, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(metricEvent);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<IBiometriaMetricEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
