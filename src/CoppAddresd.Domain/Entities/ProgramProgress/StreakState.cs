namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Contadores de racha e inventario de congelamientos de una inscripción.
/// Una fila por inscripción (PK = EnrollmentId).
/// </summary>
public sealed class StreakState
{
    public Guid EnrollmentId { get; set; }

    public int CurrentStreak { get; set; }

    public int LongestStreak { get; set; }

    /// <summary>Fecha local del último día que aportó a la racha actual.</summary>
    public DateOnly? LastActiveDate { get; set; }

    /// <summary>Congelamientos disponibles (tope 3).</summary>
    public int FreezesRemaining { get; set; }

    /// <summary>Congelamientos usados en total (solo auditoría).</summary>
    public int FreezesUsedTotal { get; set; }

    /// <summary>Fecha local en que la racha se rompió por última vez.</summary>
    public DateOnly? LastBreakDate { get; set; }

    /// <summary>
    /// Multiplicador de XP activo del paciente (SPEC §16): 1.0 = sin
    /// multiplicador, 2.0 = x2 activo. Lo activan los hitos de racha de 11/22/50
    /// días y aplica a TODOS los otorgamientos mientras está vigente (vencido →
    /// se trata como 1.0 y se resetea lazy en el próximo otorgamiento).
    /// </summary>
    public decimal MultiplierActive { get; set; } = 1.0m;

    /// <summary>
    /// Instante (UTC) en que expira el multiplicador activo. Null cuando no hay
    /// multiplicador vigente (nunca se activó o ya venció y fue reseteado).
    /// </summary>
    public DateTime? MultiplierEndsAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}