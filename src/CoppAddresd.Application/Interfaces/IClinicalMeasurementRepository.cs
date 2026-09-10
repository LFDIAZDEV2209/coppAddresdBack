using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Patients;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Acceso a las mediciones clínicas del catálogo de mediciones
/// (<see cref="Domain.Entities.ClinicalMeasurement"/>), proyectadas para el
/// contexto de IA. Las mediciones se devuelven ordenadas por fecha de
/// observación descendente (la más reciente primero).
/// </summary>
public interface IClinicalMeasurementRepository
{
    Task<IReadOnlyList<ClinicalMeasurementDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Proyección ERP de las mediciones de un paciente
    /// (erp-clinical-measurements): lista plana ordenada por
    /// <c>ObservedAt</c> desc con <c>RecordedAt</c> desc como desempate, con
    /// los campos de catálogo (<c>MetricName</c>/<c>UnitSymbol</c>) y
    /// <c>BatchId</c> derivado de los anclas de
    /// <c>task_completions.vital_signs_batch_id</c>. Set-based, sin N+1.
    /// </summary>
    Task<IReadOnlyList<PatientMeasurementDto>> ListForErpAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta un lote de mediciones clínicas (p. ej. extraídas de un examen de laboratorio)
    /// en una sola operación atómica.
    /// </summary>
    Task AddBatchAsync(IReadOnlyList<Domain.Entities.ClinicalMeasurement> measurements, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las métricas activas del catálogo con su unidad por defecto cargada.
    /// </summary>
    Task<IReadOnlyList<Domain.Entities.MeasurementMetric>> GetActiveMetricsWithUnitsAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las unidades de medida activas del catálogo.
    /// </summary>
    Task<IReadOnlyList<Domain.Entities.UnitOfMeasure>> GetActiveUnitsAsync(CancellationToken ct = default);
}