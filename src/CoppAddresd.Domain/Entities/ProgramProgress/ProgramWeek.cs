using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Semana concreta (1..83) dentro de una inscripción. Guarda una copia
/// congelada (<see cref="TasksSnapshot"/>) de la configuración de la plantilla
/// tomada al iniciar la semana; ediciones de la plantilla a mitad de semana no
/// afectan esta semana.
/// </summary>
public sealed class ProgramWeek
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    /// <summary>Número de semana dentro del programa (1..TotalWeeks).</summary>
    public int WeekNumber { get; set; }

    public ProgramWeekStatus Status { get; set; } = ProgramWeekStatus.Locked;

    /// <summary>Lunes de la semana en zona del paciente.</summary>
    public DateOnly WeekStartDateLocal { get; set; }

    /// <summary>Domingo de la semana en zona del paciente.</summary>
    public DateOnly WeekEndDateLocal { get; set; }

    /// <summary>Lista congelada de {weekday, task_code, points, sort_order} tomada al iniciar la semana (jsonb).</summary>
    public JsonElement TasksSnapshot { get; set; }

    /// <summary>Versión de <c>program_templates.version</c> vigente cuando se tomó el snapshot.</summary>
    public int TemplateVersionAtStart { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }

    public ICollection<DailyCheckIn> DailyCheckIns { get; set; } = [];

    public ICollection<TaskCompletion> TaskCompletions { get; set; } = [];
}