namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Registro de un hábito de alimentación/hidratación de un paciente en una
/// fecha local (SPEC §18, B): una fila por <c>(paciente, plantilla de hábito,
/// fecha)</c>. La crea <c>POST /api/v1/program/nutrition/log</c> de forma
/// idempotente (única por tripleta) y es la fuente de la dimensión de nutrición
/// del Índice de Salud (SPEC §13.4.3: <c>app.habit_checks</c> categoría
/// <c>alimentacion</c>) y de la adherencia semanal de los otorgamientos de
/// nutrición (SPEC §18, C).
/// </summary>
public sealed class HabitCheck
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid HabitTemplateId { get; set; }

    /// <summary>Fecha local del paciente en que se registró el hábito.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>True cuando el hábito se registró como cumplido.</summary>
    public bool IsDone { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public HabitTemplate? HabitTemplate { get; set; }
}