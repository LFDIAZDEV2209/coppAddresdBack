using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Debilidad detectada del paciente (SPEC §21, "Paso 7c"): una condición que el
/// motor determinista de reglas (ADRED-inspired) detecta en
/// <c>POST /scores/calculate</c> — o que registra un profesional — y que el
/// clínico prioriza en su cola. Una fila por hallazgo: el código de regla, la
/// categoría, la severidad, el indicador que la disparó (opcional, con su
/// métrica) y su ciclo de vida (open → acknowledged → in_intervention →
/// resolved | dismissed).
///
/// Invariantes:
/// - No se duplica mientras exista una fila <c>open</c>/<c>acknowledged</c>/
///   <c>in_intervention</c> con el mismo <c>Code</c> del paciente (AC-43).
/// - <c>ResolvedAt</c> solo se fija al transicionar a <c>resolved</c>.
/// - El origen (<c>Source</c>) nunca se reescribe por una transición de estado:
///   una fila <c>ai</c> validada por un clínico permanece <c>ai</c>.
/// </summary>
public sealed class Weakness
{
    public Guid Id { get; set; }

    /// <summary>Paciente dueño de la debilidad (<c>app.patient_profiles.id</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Código de la regla que la detectó (SPEC §21, B; p. ej. <c>WK_NUT_LOW_ADHERENCE</c>).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Eje funcional de la debilidad (SPEC §21, A).</summary>
    public WeaknessCategory Category { get; set; }

    /// <summary>Severidad ordinal para priorizar la cola clínica (default <c>low</c>).</summary>
    public WeaknessSeverity Severity { get; set; } = WeaknessSeverity.low;

    /// <summary>Título corto legible (varchar 120).</summary>
    public string Title { get; set; } = default!;

    /// <summary>Descripción del hallazgo con el indicador y la acción sugerida (puede contener contexto clínico).</summary>
    public string? Description { get; set; }

    /// <summary>Instante (reloj del servidor) en que se detectó la debilidad (default now()).</summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Métrica clínica asociada al indicador (p. ej. glucosa o % grasa); null si la regla no usa métrica.</summary>
    public Guid? MetricId { get; set; }

    /// <summary>Valor del indicador que disparó la regla (p. ej. 58 = adherencia 58%, 128 = glucosa mg/dL).</summary>
    public decimal? IndicatorValue { get; set; }

    /// <summary>Origen de la fila (default <c>ai</c>; ver SPEC §21, A).</summary>
    public WeaknessSource Source { get; set; } = WeaknessSource.ai;

    /// <summary>Ciclo de vida (default <c>open</c>; ver SPEC §21, A).</summary>
    public WeaknessStatus Status { get; set; } = WeaknessStatus.open;

    /// <summary>
    /// Clínico asignado a la intervención (<c>auth.users</c>; FK por SQL,
    /// ON DELETE SET NULL). Null hasta que alguien se asigna el caso.
    /// </summary>
    public Guid? AssignedTo { get; set; }

    /// <summary>Instante en que la debilidad quedó <c>resolved</c>; null en cualquier otro estado.</summary>
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public MeasurementMetric? Metric { get; set; }
}