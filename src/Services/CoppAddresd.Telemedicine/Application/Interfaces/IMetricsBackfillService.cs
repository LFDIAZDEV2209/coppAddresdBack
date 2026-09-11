using CoppAddresd.Telemedicine.Application.Features.Telemedicine;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Reconstruye las métricas pre-agregadas de analytics desde
/// <c>tele.appointments</c> (carga inicial, reparación de deriva, recálculo).
/// Idempotente: re-ejecutar con el mismo rango produce el mismo estado.
/// </summary>
public interface IMetricsBackfillService
{
    /// <summary>
    /// Recalcula y sobrescribe las filas pre-agregadas del rango
    /// [<paramref name="from"/>, <paramref name="to"/>] (días de agenda UTC).
    /// Con <paramref name="dryRun"/> no escribe y reporta lo que escribiría.
    /// </summary>
    Task<BackfillMetricsResult> BackfillAsync(
        DateOnly from,
        DateOnly to,
        Guid? clinicId,
        bool dryRun,
        CancellationToken ct
    );
}
