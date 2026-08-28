using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Valor base de una métrica clínica del paciente (SPEC §13.1.2): define qué
/// significa "mejoría" para el Índice de Transformación. Solo un clínico puede
/// crearla (<c>set_by</c> debe resolver a un rol clínico, AC-22); un paciente
/// jamás se auto-asigna una línea base.
/// </summary>
public sealed class ClinicalBaseline
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Métrica del catálogo (<c>app.measurement_metrics</c>).</summary>
    public Guid MetricId { get; set; }

    /// <summary>Valor base en la unidad indicada (<c>numeric(10,4)</c>).</summary>
    public decimal Value { get; set; }

    /// <summary>Unidad del valor (<c>app.unit_of_measures</c>).</summary>
    public Guid UnitId { get; set; }

    /// <summary>Dirección favorable: -1 bajar es mejor, +1 subir es mejor.</summary>
    public FavorableDirection FavorableDirection { get; set; }

    /// <summary>Meta clínica opcional. Requiere <c>target_value > 0</c> y validación de rango.</summary>
    public decimal? TargetValue { get; set; }

    /// <summary>Fecha local del paciente que refleja la línea base.</summary>
    public DateOnly MeasuredAt { get; set; }

    /// <summary>
    /// Usuario de <c>auth.users</c> que fijó la línea base. NOT NULL con FK por
    /// SQL (sin navegación EF, patrón <c>app.patient_profiles</c>): debe ser un
    /// clínico (AC-22).
    /// </summary>
    public Guid SetBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public MeasurementMetric? Metric { get; set; }

    public UnitOfMeasure? Unit { get; set; }
}