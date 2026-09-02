using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Fila del catálogo de plantilla: una entrada por (template, weekday, task_code)
/// que define que un tipo de tarea corre ese día de la semana con sus puntos base.
/// </summary>
public sealed class WeeklyDayTemplate
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Día de la semana: 1 = lunes … 7 = domingo (mismo índice que NutritionPlanDay).</summary>
    public short Weekday { get; set; }

    /// <summary>Código de tarea: podcast, vitals, nut, ejercicio, nutraceutico, emocional.</summary>
    public TaskCode TaskCode { get; set; }

    /// <summary>Puntos base que otorga esta tarea.</summary>
    public int Points { get; set; }

    /// <summary>Orden de la tarea dentro del día (orden de UI).</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Contenido multimedia (podcast) por defecto a nivel plantilla: fallback
    /// P1 de resolución de contenido cuando no hay <c>media_progressions</c>
    /// (SPEC §4.4). Nullable; el seeder lo puebla para el task <c>podcast</c>.
    /// </summary>
    public Guid? MediaId { get; set; }

    /// <summary>Rutina de ejercicio específica para este día (opcional).</summary>
    public Guid? RoutineId { get; set; }

    /// <summary>Plan nutricional específico para este día (opcional).</summary>
    public Guid? NutritionPlanId { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que creó la fila (auditoría, sin navegación EF).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProgramTemplate? Template { get; set; }

    public MediaItem? Media { get; set; }
}