namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Plantilla de hábito de alimentación/hidratación (SPEC §18): catálogo
/// sembrado de comidas (<c>des</c>/<c>alm</c>/<c>mer</c>/<c>cen</c>) e
/// hidratación (<c>agua</c>) que la app móvil registra por fecha local vía
/// <c>app.habit_checks</c>. La categoría alimenta la dimensión de nutrición del
/// Índice de Salud (SPEC §13.4.3: <c>category = 'alimentacion'</c>) y los
/// otorgamientos granulares de nutrición (SPEC §18, B/C).
/// </summary>
public sealed class HabitTemplate
{
    public Guid Id { get; set; }

    /// <summary>
    /// Código canónico = código de comida del móvil (<c>des</c>/<c>alm</c>/
    /// <c>mer</c>/<c>cen</c>/<c>agua</c>). Único; clave del UPSERT del seeder.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Categoría: <c>alimentacion</c> (comidas) o <c>agua</c> (hidratación).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Orden de la UI del móvil dentro de la categoría.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HabitCheck> HabitChecks { get; set; } = [];
}