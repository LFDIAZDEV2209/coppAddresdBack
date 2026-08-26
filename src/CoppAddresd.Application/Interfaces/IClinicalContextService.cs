using CoppAddresd.Application.DTOs.Ai;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Consolida el contexto clínico de un paciente para la generación de planes
/// con IA: perfil demográfico, estilo de vida, alergias, diagnósticos (ICD-10),
/// medicamentos y mediciones consolidadas (ClinicalMeasurement con fallback a
/// VitalSign por métrica). Sin N+1: cada origen se carga con una sola query.
/// </summary>
public interface IClinicalContextService
{
    Task<ClinicalContextDto> ConsolidateAsync(Guid patientId, CancellationToken ct = default);
}