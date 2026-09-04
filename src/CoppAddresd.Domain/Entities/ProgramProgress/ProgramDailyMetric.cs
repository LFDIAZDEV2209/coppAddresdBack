namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Tabla de resumen dimensional y pre-agregación para el módulo de Programa ANTARES (schema app).
/// Permite consultas de dashboards en O(1) evitando escaneos masivos sobre tablas transaccionales.
/// </summary>
public sealed class ProgramDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid? ClinicId { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public string DimensionKey { get; set; } = "general";
    public long TotalCount { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
