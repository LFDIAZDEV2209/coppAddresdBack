using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Entrada de libro mayor de XP (append-only). <see cref="BalanceAfter"/> es un
/// denormalizado para lecturas baratas de UI y debe reconciliarse con la suma
/// de <see cref="Amount"/> de la inscripción.
/// </summary>
public sealed class XpLedgerEntry
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    /// <summary>Siempre positivo en MVP (no hay revocaciones).</summary>
    public int Amount { get; set; }

    public XpReason Reason { get; set; }

    /// <summary>Mismo dominio que <c>task_completions.source_ref_type</c>.</summary>
    public string? SourceRefType { get; set; }

    /// <summary>Referencia de origen (ej: id de una task_completion).</summary>
    public Guid? SourceRefId { get; set; }

    /// <summary>
    /// Código de la regla del catálogo <c>app.xp_rules</c> que produjo esta
    /// entrada (provenance, SPEC §14). Null cuando la entrada se otorgó sin
    /// regla vigente (fallback al comportamiento por defecto) o antes de la
    /// existencia del catálogo. Es prospective: editar la regla nunca
    /// reescribe esta columna ni el <see cref="Amount"/>.
    /// </summary>
    public string? RuleCode { get; set; }

    /// <summary>Balance de XP de la inscripción luego de esta entrada.</summary>
    public int BalanceAfter { get; set; }

    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Usuario de <c>auth.users</c> que otorgó la XP; null en autocompletados (sin navegación EF).</summary>
    public Guid? GrantedBy { get; set; }

    /// <summary>
    /// Usuario de <c>auth.users</c> que validó el otorgamiento (SPEC §15): se
    /// fija SOLO cuando una regla <c>requires_validation = true</c> (p. ej.
    /// <c>CLINICAL_SIGNIFICANT</c>) fue aprobada por un clínico. Null cuando la
    /// regla no requiere validación (las auto-otorgadas como
    /// <c>CLINICAL_IMPROVE</c>/<c>CLINICAL_STABLE</c> quedan null) o cuando el
    /// otorgamiento está pendiente de validación. FK por SQL a
    /// <c>auth."Users"</c> (sin navegación EF), ON DELETE SET NULL.
    /// </summary>
    public Guid? ValidatedBy { get; set; }

    /// <summary>
    /// Instante en que un clínico validó el otorgamiento (SPEC §15). Null
    /// mientras la validación no ocurrió. Los totales de XP excluyen las filas
    /// cuya regla requiere validación y aún no fueron validadas (no cuentan
    /// hasta aprobarse).
    /// </summary>
    public DateTime? ValidatedAt { get; set; }

    /// <summary>
    /// Multiplicador EFECTIVO aplicado a este otorgamiento (SPEC §16, C):
    /// <c>multiplicador de la regla × multiplicador del paciente</c>. Null en
    /// filas previas a la existencia de la columna (prospective only, nunca se
    /// reescribe historial). 1.0 cuando no hubo multiplicador.
    /// </summary>
    public decimal? MultiplierUsed { get; set; }

    /// <summary>Regla del catálogo que produjo la entrada (provenance, SPEC §14).</summary>
    public XpRule? Rule { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}