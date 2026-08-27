using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Plantilla de programa reutilizable (83 semanas por defecto). Define qué
/// tareas corren cada día de la semana a través de <see cref="WeeklyDayTemplate"/>
/// y se versiona en cada publicación. Las semanas ya activas congelan su
/// snapshot al iniciar, por lo que editar la plantilla no afecta semanas en curso.
/// </summary>
public sealed class ProgramTemplate
{
    public Guid Id { get; set; }

    /// <summary>Código único (ej: <c>default-83w</c>).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Duración total del programa en semanas (por defecto 83).</summary>
    public int TotalWeeks { get; set; } = 83;

    /// <summary>Estado del ciclo de vida: Draft / Active / Archived.</summary>
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;

    /// <summary>Se incrementa en cada publicación; las ediciones en borrador no lo cambian.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Mínimo de tareas completadas por día (fecha local del paciente) para
    /// mantener la racha (SPEC §17, B): un día "cumple el umbral" si
    /// <c>tasks_done &gt;= StreakMinTasks</c>. Default 1 = cualquier día con al
    /// menos una tarea mantiene la racha (comportamiento previo). Se lee desde
    /// <c>app.program_templates</c> vía <c>program_enrollments.program_template_id</c>.
    /// </summary>
    public short StreakMinTasks { get; set; } = 1;

    /// <summary>
    /// Códigos de tarea que cuentan como "esenciales" para el rescate con
    /// congelamiento (SPEC §17, C; referencia ADRED: <c>nut</c>,
    /// <c>ejercicio</c>, <c>nutribiotico</c>). Un día bajo el umbral solo puede
    /// rescatarse con un congelamiento si completó al menos una tarea esencial.
    /// Lista vacía = sin restricción (comportamiento previo). Se persiste como
    /// <c>jsonb</c>.
    /// </summary>
    public List<string> EssentialTaskCodes { get; set; } = [];

    /// <summary>Usuario de <c>auth.users</c> que creó la plantilla (auditoría, sin navegación EF).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Último usuario de <c>auth.users</c> que modificó la plantilla (auditoría, sin navegación EF).</summary>
    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Momento en que la plantilla pasó a estado Active.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Definición por día de la semana: qué tareas corren y con qué puntos.</summary>
    public ICollection<WeeklyDayTemplate> DayTemplates { get; set; } = [];
}