using System.Threading.Channels;
using CoppAddresd.Application.Features.ProgramProgress.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Implementación de cola en memoria basada en System.Threading.Channels con SingleReader optimizado.
/// </summary>
public sealed class ProgramMetricsQueue : IProgramMetricsQueue
{
    private readonly Channel<IProgramMetricEvent> _channel = Channel.CreateUnbounded<IProgramMetricEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(IProgramMetricEvent metricEvent, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(metricEvent, ct);
    }

    public IAsyncEnumerable<IProgramMetricEvent> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
