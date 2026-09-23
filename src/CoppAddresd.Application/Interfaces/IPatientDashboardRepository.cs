using CoppAddresd.Application.Features.Patients;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Consultas agregadas del dashboard general de pacientes y del tablero
/// clínico. El alcance (clínica activa + global vs propio) se resuelve en el
/// backend y todas las proyecciones se calculan sin consultas por fila
/// (agregados en SQL + consultas bulk para la página).
/// </summary>
public interface IPatientDashboardRepository
{
    /// <summary>
    /// Agregados del dashboard: KPIs (misma fuente que stats), demografía,
    /// crecimiento mensual, top de profesionales (solo alcance global) y
    /// distribución por estado. <paramref name="stateCode"/> acota demografía,
    /// crecimiento y top de profesionales; la geografía viaja completa.
    /// </summary>
    Task<PatientDashboardDto> GetDashboardAsync(
        Guid? clinicId,
        Guid? professionalId,
        string? stateCode,
        int months,
        DateTime nowUtc,
        CancellationToken ct = default
    );

    /// <summary>
    /// Página del tablero clínico con búsqueda y filtros de riesgo, alertas,
    /// seguimiento, estado del paciente, aseguradora y estado geográfico.
    /// Devuelve las filas, el total para la paginación y el resumen por buckets
    /// (este último sin los filtros clínicos, para las tarjetas del tab).
    /// </summary>
    Task<(
        IReadOnlyList<ClinicalBoardItemDto> Items,
        int Total,
        ClinicalBoardSummaryDto Summary
    )> GetClinicalBoardAsync(
        int page,
        int pageSize,
        string? search,
        string? risk,
        bool? hasAlerts,
        string? followUp,
        string? status,
        Guid? insurerId,
        string? stateCode,
        Guid? clinicId,
        Guid? professionalId,
        DateTime nowUtc,
        CancellationToken ct = default
    );
}
