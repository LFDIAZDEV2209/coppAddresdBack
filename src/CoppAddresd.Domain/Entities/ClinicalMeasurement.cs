namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Medición clínica de un paciente: el valor de una métrica en una unidad, con
/// su contexto temporal y de origen.
/// <see cref="ObservedAt"/> indica cuándo se tomó la medición y
/// <see cref="RecordedAt"/> cuándo se registró en el sistema (difieren al cargar
/// históricos en una consulta). Un <see cref="EncounterId"/> null significa
/// monitoreo autónomo (dispositivo o auto-reporte del paciente). ADR-002.
/// </summary>
public sealed class ClinicalMeasurement
{
    public Guid Id { get; set; }

    /// <summary>Paciente al que pertenece la medición.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Métrica medida (catálogo).</summary>
    public Guid MetricId { get; set; }

    /// <summary>Encounter de origen. Null = monitoreo autónomo (sin visita).</summary>
    public Guid? EncounterId { get; set; }

    /// <summary>Valor de la medición en la unidad indicada.</summary>
    public decimal Value { get; set; }

    /// <summary>Unidad del valor (catálogo).</summary>
    public Guid UnitId { get; set; }

    /// <summary>Cuándo se tomó la medición.</summary>
    public DateTime ObservedAt { get; set; }

    /// <summary>Cuándo se registró en el sistema. Default now().</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Origen: <c>device</c> | <c>patient</c> | <c>professional</c> | <c>lab</c>.</summary>
    public string Source { get; set; } = default!;

    /// <summary>Lote de examen que originó la medición. Null si no proviene de lab upload.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>
    /// Clave S3 del archivo fuente. Formato: lab-exams/{patientId}/{batchId}.{ext}
    /// </summary>
    public string? SourceKey { get; set; }

    public string? Notes { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que registró la medición (auditoría).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Paciente al que pertenece la medición.</summary>
    public PatientProfile? Patient { get; set; }

    /// <summary>Métrica medida.</summary>
    public MeasurementMetric? Metric { get; set; }

    /// <summary>Encounter de origen, si la medición se registró en una visita.</summary>
    public Encounter? Encounter { get; set; }

    /// <summary>Unidad del valor.</summary>
    public UnitOfMeasure? Unit { get; set; }
}