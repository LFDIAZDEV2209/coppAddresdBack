using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Fila de auditoría por cada congelamiento de racha otorgado, consumido o
/// expirado dentro de una inscripción.
/// </summary>
public sealed class StreakFreeze
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public StreakFreezeKind Kind { get; set; }

    /// <summary>Fecha local en que se consumió; solo para <see cref="StreakFreezeKind.Consumed"/>.</summary>
    public DateOnly? UsedOnLocalDate { get; set; }

    /// <summary>Momento en que se otorgó; solo para <see cref="StreakFreezeKind.Granted"/>.</summary>
    public DateTime? GrantedAt { get; set; }

    /// <summary>Motivo del otorgamiento: PerfectWeekBonus, AdaptationApproval o Manual.</summary>
    public string GrantedReason { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProgramEnrollment? Enrollment { get; set; }
}