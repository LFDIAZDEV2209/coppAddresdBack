using System.Threading.Channels;
using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Implementación de cola en memoria de alto rendimiento usando Channel delimitado (BoundedChannel).
/// Emite y consume eventos con cero contención de locks y sin impactar la latencia de las transacciones HTTP.
/// </summary>
public sealed class PatientMetricsQueue : IPatientMetricsQueue
{
    private readonly Channel<IPatientMetricEvent> _channel = Channel.CreateBounded<IPatientMetricEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        }
    );

    public ValueTask EnqueueAsync(IPatientMetricEvent metricEvent, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(metricEvent);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<IPatientMetricEvent> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
