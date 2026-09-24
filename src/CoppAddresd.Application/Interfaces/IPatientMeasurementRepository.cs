using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Lectura paginada de mediciones clínicas propias para el móvil
/// (self-service <c>GET /api/v1/me/measurements</c>, Fase 7). La
/// implementación EF vive en Infrastructure (siguiente tarea): una sola query
/// set-based sobre <c>app.clinical_measurements</c> con joins de catálogo
/// (métrica y unidad), orden estable y cursor opaco. Solo proyección mínima
/// (<see cref="Features.Measurements.Queries.GetMyMeasurements.MeasurementItemDto"/>),
/// <c>AsNoTracking</c>, sin N+1.
/// </summary>
public interface IPatientMeasurementRepository
{
    /// <summary>
    /// Página de mediciones del paciente, orden estable (la define la
    /// implementación). <paramref name="metricCodes"/> null o vacío = sin
    /// filtro (todas las métricas). <paramref name="cursor"/> null = primera
    /// página; cualquier otro valor es el <c>NextCursor</c> opaco de la página
    /// anterior. Nunca devuelve null: sin filas → página vacía.
    /// </summary>
    Task<Features.Measurements.Queries.GetMyMeasurements.CursorPagedResult<Features.Measurements.Queries.GetMyMeasurements.MeasurementItemDto>> GetPagedAsync(
        Guid patientId,
        string[]? metricCodes,
        int pageSize,
        string? cursor,
        CancellationToken ct
    );
}
