namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Regla del catálogo de XP (SPEC §14): configuración data-driven de cómo se
/// otorgan los puntos del módulo, con topes anti-fraude por día/semana.
///
/// Precedencia (SPEC §14.3): si existe una regla <c>Active</c> dentro de su
/// ventana <see cref="ValidFrom"/>..<see cref="ValidUntil"/>, gana sobre el
/// comportamiento por defecto; si no existe o está inactiva/vencida, el
/// otorgamiento cae al comportamiento actual (puntos de plantilla, sin
/// límites). Las ediciones son <b>prospective only</b>: nunca reescriben el
/// historial de <c>app.xp_ledger</c> (la columna <c>rule_code</c> solo
/// documenta la regla que produjo cada entrada).
/// </summary>
public sealed class XpRule
{
    public Guid Id { get; set; }

    /// <summary>Código único de la regla (ej: <c>TASK_PODCAST</c>, <c>DAY_BONUS</c>, <c>STREAK_7</c>).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Nombre legible de la regla (ej: "Tarea podcast").</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Categoría de la regla: <c>adherence</c> (tareas + bonus de día perfecto)
    /// o <c>streak</c> (hitos de racha).
    /// </summary>
    public string Category { get; set; } = default!;

    /// <summary>
    /// Puntos base de la regla. <c>null</c> significa "diferir a la fuente de
    /// puntos existente": para las reglas <c>TASK_*</c> se usan los puntos del
    /// snapshot (<c>weekly_day_templates.points</c>).
    /// </summary>
    public int? BaseXp { get; set; }

    /// <summary>Multiplicador aplicado sobre la base (total = floor(base × multiplier)). Default 1.0.</summary>
    public decimal Multiplier { get; set; } = 1.0m;

    /// <summary>Tope anti-fraude de otorgamientos por día (null = sin límite).</summary>
    public int? MaxPerDay { get; set; }

    /// <summary>Tope anti-fraude de otorgamientos por semana (null = sin límite).</summary>
    public int? MaxPerWeek { get; set; }

    /// <summary>
    /// La regla requiere validación externa antes de otorgar (reservado; el
    /// MVP no implementa validadores de terceros).
    /// </summary>
    public bool RequiresValidation { get; set; }

    /// <summary>La regla está vigente (las reglas inactivas caen al fallback).</summary>
    public bool Active { get; set; } = true;

    /// <summary>Inicio de vigencia (fecha local del servidor, default hoy).</summary>
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Fin de vigencia (null = vigente sin fecha límite).</summary>
    public DateOnly? ValidUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}