using CoppAddresd.Application.Features.Patients.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cola en memoria no bloqueante para eventos de métricas de pacientes (Channel pattern).
/// Desacopla la transacción de registro/edición del procesamiento analítico en segundo plano.
/// </summary>
public interface IPatientMetricsQueue
{
    ValueTask EnqueueAsync(IPatientMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IPatientMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
