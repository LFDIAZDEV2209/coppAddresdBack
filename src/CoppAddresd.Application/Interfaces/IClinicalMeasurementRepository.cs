using CoppAddresd.Application.DTOs.Ai;

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
}