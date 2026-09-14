namespace CoppAddresd.Application.Features.HealthTests;

/// <summary>
/// DTO geográfico para el mapa de Tests de Salud: agrega pacientes por ciudad
/// con métricas de riesgo derivadas de health_test_results. <c>Count</c> es el
/// total de pacientes mapeados y <c>EvaluatedCount</c> los que tienen
/// evaluaciones con score (base del porcentaje de riesgo).
/// </summary>
public record HealthTestsGeoCityDto(
    Guid? CityId,
    string Name,
    string? StateAbbr,
    int Count,
    int EvaluatedCount,
    double? HighRiskPct,
    double? AvgScore,
    double? MapX,
    double? MapY
);

public record HealthTestsGeoAlertDto(
    Guid PatientId,
    string Name,
    string Reason,
    double? Score,
    string? Severity
);

public record HealthTestsGeoDto(
    IReadOnlyList<HealthTestsGeoCityDto> Cities,
    IReadOnlyList<HealthTestsGeoAlertDto> Alerts,
    int TotalPatients,
    double? AvgScore,
    int HighRiskCount
);
