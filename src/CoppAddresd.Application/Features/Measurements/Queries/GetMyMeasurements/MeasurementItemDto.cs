namespace CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;

/// <summary>
/// Fila mínima de una medición clínica para el móvil (self-service
/// <c>GET /api/v1/me/measurements</c>, Fase 7). Solo campos de catálogo y
/// lectura: código/nombre de métrica, valor, código/símbolo de unidad, fecha
/// de observación y origen. No incluye notas clínicas, <c>createdBy</c>,
/// <c>encounterId</c>, <c>batchId</c> ni <c>sourceKey</c> (el móvil no los
/// necesita y se minimiza la exposición de PHI).
/// </summary>
public sealed record MeasurementItemDto(
    Guid Id,
    string MetricCode,
    string MetricName,
    decimal Value,
    string UnitCode,
    string UnitSymbol,
    DateTimeOffset ObservedAt,
    string Source
);
