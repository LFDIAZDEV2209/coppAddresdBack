namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Tabla de rollup tipada para la actividad y rendimiento diario de cada médico (schema tele).
/// Alimenta la tabla de desempeño de profesionales en el dashboard sin escaneos pesados.
/// </summary>
public sealed class ProfessionalDailyStat
{
    public Guid ProfessionalId { get; set; }

    public DateOnly MetricDate { get; set; }

    public Guid? ClinicId { get; set; }

    public int TotalAppointments { get; set; }

    public int CompletedAppointments { get; set; }

    public int CancelledAppointments { get; set; }

    public int NoShowAppointments { get; set; }

    public int UniquePatients { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
