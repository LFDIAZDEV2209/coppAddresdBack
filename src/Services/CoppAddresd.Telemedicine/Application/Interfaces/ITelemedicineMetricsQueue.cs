using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Canal asíncrono en memoria para métricas de telemedicina (0ms de latencia agregada al agendar).
/// </summary>
public interface ITelemedicineMetricsQueue
{
    ValueTask EnqueueAsync(ITelemedicineMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<ITelemedicineMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
