namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Métrica diaria pre-agregada para el módulo de pacientes y dashboard general del ERP (schema app).
/// Almacena conteos por fecha, clínica, métrica y dimensión en O(1).
/// </summary>
public sealed class PatientDailyMetric
{
    public DateOnly MetricDate { get; set; }

    /// <summary>
    /// ID de la clínica. Usa Guid.Empty para métricas globales consolidadas (vista Admin).
    /// </summary>
    public Guid ClinicId { get; set; } = Guid.Empty;

    public string MetricKey { get; set; } = string.Empty;

    public string DimensionKey { get; set; } = "general";

    public long TotalCount { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
