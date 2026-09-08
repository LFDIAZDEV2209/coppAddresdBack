using System.Threading.Channels;
using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Implementación de cola no bloqueante usando Channel delimitado para métricas de Tests de Salud.
/// </summary>
public sealed class HealthTestMetricsQueue : IHealthTestMetricsQueue
{
    private readonly Channel<IHealthTestMetricEvent> _channel = Channel.CreateBounded<IHealthTestMetricEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ValueTask EnqueueAsync(IHealthTestMetricEvent metricEvent, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(metricEvent);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<IHealthTestMetricEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
