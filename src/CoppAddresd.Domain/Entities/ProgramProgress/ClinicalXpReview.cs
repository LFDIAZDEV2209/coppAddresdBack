using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Revisión clínica pendiente de XP (SPEC §15): cuando el motor detecta una
/// mejoría significativa (|Δ%| ≥ umbral, p. ej. 5%) de una métrica clínica
/// frente a su línea base, NO otorga XP automáticamente: crea una fila
/// <c>pending</c> que un clínico debe decidir (aprobar → se otorga
/// <c>CLINICAL_SIGNIFICANT</c> con <c>validated_by</c>/<c>validated_at</c>;
/// rechazar → no se otorga nada). Una fila por
/// <c>(patient_id, health_score_id, metric_id)</c> (período × métrica).
/// </summary>
public sealed class ClinicalXpReview
{
    public Guid Id { get; set; }

    /// <summary>Paciente (<c>app.patient_profiles</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Fila de <c>app.health_scores</c> del período en que se detectó la
    /// mejoría; también es el <c>source_ref_id</c> del otorgamiento clínico en
    /// <c>xp_ledger</c> (dedupe parcial <c>(source_ref_type, source_ref_id,
    /// reason)</c>, SPEC §15).
    /// </summary>
    public Guid HealthScoreId { get; set; }

    /// <summary>Métrica clínica evaluada (<c>app.measurement_metrics</c>).</summary>
    public Guid MetricId { get; set; }

    /// <summary>
    /// Código de regla que se otorgará si se aprueba (default
    /// <c>CLINICAL_SIGNIFICANT</c>; el catálogo la tiene con
    /// <c>requires_validation = true</c>).
    /// </summary>
    public string RuleCode { get; set; } = XpRuleCodes.ClinicalSignificant;

    /// <summary>Variación porcentual observada de la métrica en el período (|Δ%|, <c>numeric(8,3)</c>).</summary>
    public decimal? DeltaPct { get; set; }

    /// <summary>
    /// Estado de la revisión: <c>pending</c> (recién detectada) →
    /// <c>approved</c> (aprobada: se otorga XP) o <c>rejected</c> (sin XP).
    /// </summary>
    public ClinicalXpReviewStatus Status { get; set; } = ClinicalXpReviewStatus.pending;

    /// <summary>
    /// Clínico que decidió (<c>auth.users</c>; FK por SQL, ON DELETE SET NULL).
    /// Null mientras la revisión está pendiente.
    /// </summary>
    public Guid? DecidedBy { get; set; }

    /// <summary>Instante de la decisión; null mientras está pendiente.</summary>
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public HealthScore? HealthScore { get; set; }

    public MeasurementMetric? Metric { get; set; }
}