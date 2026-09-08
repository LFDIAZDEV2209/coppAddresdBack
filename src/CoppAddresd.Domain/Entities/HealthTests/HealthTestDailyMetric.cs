namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Métrica diaria pre-agregada para el módulo de Tests de Salud y Baterías Clínicas (schema app).
/// Almacena estados de asignaciones, severidades, alertas y ejecuciones en O(1).
/// </summary>
public sealed class HealthTestDailyMetric
{
    public DateOnly MetricDate { get; set; }

    /// <summary>
    /// ID de la clínica o Guid.Empty para métricas globales agregadas (vista Admin).
    /// </summary>
    public Guid ClinicId { get; set; } = Guid.Empty;

    public string MetricKey { get; set; } = string.Empty;

    public string DimensionKey { get; set; } = "general";

    public long TotalCount { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
