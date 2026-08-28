using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Intervención derivada de una debilidad del paciente (SPEC §22, "Paso 7d"):
/// cuando el motor de detección de debilidades (SPEC §21) dispara una regla
/// con acción <c>create_intervention</c> o <c>telehealth_referral</c>, se crea
/// una fila en <c>app.interventions</c> que el clínico y el paciente gestionan
/// a través de su máquina de estados.
///
/// Invariantes:
/// - Una intervención se crea con estado <c>detected</c>.
/// - El paciente solo puede transicionar <c>detected</c> → <c>accepted</c>.
/// - Un <c>weakness_id</c> vincula la intervención a su debilidad origen; solo
///   puede haber una intervención por debilidad (el repositorio verifica antes
///   de crear, AC-46).
/// - <c>xp_awarded_total</c> se actualiza con cada otorgamiento de XP vinculado
///   a la intervención ( nunca se decrementa, solo se acumula).
/// - Las transiciones inválidas producen <c>409 CONFLICT</c>.
/// </summary>
public sealed class Intervention
{
    public Guid Id { get; set; }

    /// <summary>Paciente dueño de la intervención (<c>app.patient_profiles.id</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Debilidad origen (opcional; nullable FK a <c>app.weaknesses.id</c>,
    /// ON DELETE SET NULL). La intervención puede crearse directamente por
    /// un profesional sin debilidad asociada.
    /// </summary>
    public Guid? WeaknessId { get; set; }

    /// <summary>Tipo de intervención (SPEC §22, A).</summary>
    public InterventionType Type { get; set; }

    /// <summary>Título corto legible (varchar 120).</summary>
    public string Title { get; set; } = default!;

    /// <summary>Descripción detallada de la intervención (text, nullable).</summary>
    public string? Description { get; set; }

    /// <summary>Estado actual de la máquina de estados (default <c>detected</c>).</summary>
    public InterventionStatus Status { get; set; } = InterventionStatus.detected;

    /// <summary>Severidad heredada de la debilidad o asignada por el clínico (default <c>medium</c>).</summary>
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// Clínico/profesional asignado a la intervención (<c>auth.users</c>; FK
    /// por SQL en la migración, ON DELETE SET NULL). Null hasta que se asigna.
    /// </summary>
    public Guid? AssignedTo { get; set; }

    /// <summary>Instante en que se recomendó la intervención al paciente.</summary>
    public DateTime? RecommendedAt { get; set; }

    /// <summary>Instante en que el paciente aceptó la intervención.</summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>Instante en que la intervención se completó.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Acción que el paciente debe realizar (p. ej. "Agendar teleconsulta",
    /// "Completar cuestionario"; varchar 120, nullable).
    /// </summary>
    public string? PatientAction { get; set; }

    /// <summary>Resultado de la intervención cuando se completa (text, nullable).</summary>
    public string? Result { get; set; }

    /// <summary>
    /// Total acumulado de XP otorgada por esta intervención (default 0).
    /// Se actualiza con cada otorgamiento (WEAKNESS_ASSESS, INTERV_ACCEPT,
    /// TELE_SCHEDULE, TELE_ATTEND, TELE_COMPLY, INTERV_COMPLETE, RECOVERY_MISSION).
    /// </summary>
    public int XpAwardedTotal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // --- Navegaciones (sin tracking de auth.users; assigned_to es FK por SQL) ---
    public PatientProfile? Patient { get; set; }
    public Weakness? Weakness { get; set; }
}
