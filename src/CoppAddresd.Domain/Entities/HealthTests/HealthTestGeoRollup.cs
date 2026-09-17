namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Rollup snapshot por ciudad para el dashboard geográfico de Tests de Salud
/// (schema app). Representa el estado ACTUAL de cada ciudad (no una serie
/// temporal): pacientes con ciudad, evaluados con score, alto riesgo (peor
/// severidad high/critical por paciente), alertas activas y promedios de score.
/// Alimentada por recomputo set-based idempotente (backfill al arranque +
/// recomputo de la ciudad afectada en el processor de eventos).
/// avg_score_sum/avg_score_count guardan la suma de promedios por paciente y el
/// número de pacientes evaluados: el promedio global es SUM(suma)/SUM(conteo).
/// </summary>
public sealed class HealthTestGeoRollup
{
    public Guid CityId { get; set; }

    public string StateCode { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public long PatientsCount { get; set; }

    public long EvaluatedCount { get; set; }

    public long HighRiskCount { get; set; }

    public long ActiveAlertsCount { get; set; }

    public decimal AvgScoreSum { get; set; }

    public long AvgScoreCount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
