namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Medición de signos vitales de un paciente (una fila por toma). Los valores
/// se almacenan en unidades SI (cm, kg, °C); la conversión de unidades de
/// origen ocurre en el borde de importación, nunca en el dominio.
/// </summary>
public sealed class VitalSign
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public DateTime MeasuredAt { get; set; }

    public int? Systolic { get; set; }

    public int? Diastolic { get; set; }

    public int? HeartRate { get; set; }

    /// <summary>Temperatura corporal en °C.</summary>
    public decimal? TemperatureC { get; set; }

    /// <summary>Saturación de oxígeno en %.</summary>
    public int? O2Saturation { get; set; }

    /// <summary>Altura en cm.</summary>
    public decimal? HeightCm { get; set; }

    /// <summary>Peso en kg.</summary>
    public decimal? WeightKg { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }
}