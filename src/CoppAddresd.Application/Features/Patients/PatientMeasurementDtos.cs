namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Medición clínica de un paciente para el panel ERP (solo lectura). Es el
/// contrato plano del endpoint GET /api/v1/patients/{id}/measurements: campos
/// de catálogo desnormalizados (MetricName/UnitSymbol) y la clave de
/// agrupación <c>BatchId</c> (ancla del lote de check-in; null = fila huérfana
/// que el frontend renderiza en su propia tarjeta). <c>SourceKey</c> es la
/// clave S3 del archivo origen cuando la fila proviene de una carga de examen
/// (contexto del documento del lote, UC-004).
///
/// Serialización camelCase por default de ASP.NET Core (el API solo agrega
/// <c>JsonStringEnumConverter</c>); <c>ObservedAt</c> viaja en ISO-8601 UTC.
/// </summary>
public record PatientMeasurementDto(
    Guid Id,
    string MetricCode,
    string MetricName,
    decimal Value,
    string UnitCode,
    string UnitSymbol,
    DateTime ObservedAt,
    string Source,
    Guid? BatchId,
    string? SourceKey);