using System.Threading.Channels;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Application.Interfaces;

namespace CoppAddresd.Telemedicine.Infrastructure.Metrics;

/// <summary>
/// Canal asíncrono en memoria para eventos de telemedicina con SingleReader optimizado.
/// </summary>
public sealed class TelemedicineMetricsQueue : ITelemedicineMetricsQueue
{
    private readonly Channel<ITelemedicineMetricEvent> _channel = Channel.CreateUnbounded<ITelemedicineMetricEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(ITelemedicineMetricEvent metricEvent, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(metricEvent, ct);
    }

    public IAsyncEnumerable<ITelemedicineMetricEvent> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
