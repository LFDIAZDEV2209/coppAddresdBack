namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Resumen diario de una inscripción: rollup de ánimo, barreras, puntos del día
/// y si fue un día perfecto (todas las tareas programadas completadas).
/// </summary>
public sealed class DailyCheckIn
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public Guid ProgramWeekId { get; set; }

    /// <summary>Fecha local del paciente.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>Día de la semana (1..7), denormalizado para filtros baratos.</summary>
    public short Weekday { get; set; }

    /// <summary>Ánimo reportado (1..5) si se completó la tarea emocional.</summary>
    public short? MoodScore { get; set; }

    /// <summary>Barrera reportada (id de <c>WEEK_BARRIERS</c> del móvil); solo si el paciente la reporta.</summary>
    public string? Barriers { get; set; }

    /// <summary>Suma de <c>task_completions.points_awarded</c> del día.</summary>
    public int TotalPoints { get; set; }

    /// <summary>Bonus de día perfecto otorgado (default 50; ajustable vía regla <c>DAY_BONUS</c>, SPEC §14).</summary>
    public int BonusAwarded { get; set; }

    /// <summary>True si se completaron todas las tareas programadas del día.</summary>
    public bool IsPerfectDay { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }

    public ProgramWeek? Week { get; set; }

    public ICollection<TaskCompletion> TaskCompletions { get; set; } = [];
}