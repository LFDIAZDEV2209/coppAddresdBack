namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Tabla de resumen dimensional y pre-agregación para el módulo de Citas y Telemedicina (schema tele).
/// Almacena métricas de estados, distribuciones horarias y conteos por día en O(1).
/// </summary>
public sealed class AppointmentDailyMetric
{
    public DateOnly MetricDate { get; set; }

    /// <summary>
    /// ID del profesional. Usa Guid.Empty (00000000-0000-0000-0000-000000000000)
    /// para las métricas globales agregadas de la clínica/sistema (vista Admin).
    /// </summary>
    public Guid ProfessionalId { get; set; } = Guid.Empty;

    public Guid? ClinicId { get; set; }

    public string MetricKey { get; set; } = string.Empty;

    public string DimensionKey { get; set; } = "general";

    public long TotalCount { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
