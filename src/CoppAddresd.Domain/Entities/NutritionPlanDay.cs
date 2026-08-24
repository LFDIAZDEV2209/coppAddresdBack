using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Día y comida de un plan de alimentación. Cada plan tiene N días,
/// y cada día tiene 1-4 comidas (desayuno, almuerzo, cena, snack).
/// </summary>
public sealed class NutritionPlanDay
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    /// <summary>Número del día dentro del plan (1, 2, 3...).</summary>
    public int DayNumber { get; set; }

    public MealType MealType { get; set; }

    /// <summary>Descripción de la comida (ej. "Ensalada de pollo con aguacate").</summary>
    public string? Description { get; set; }

    /// <summary>Lista de alimentos/ingredientes.</summary>
    public string? Foods { get; set; }

    /// <summary>Calorías estimadas de esta comida.</summary>
    public int? Calories { get; set; }

    /// <summary>Notas adicionales (porciones, preparación, etc.).</summary>
    public string? Notes { get; set; }

    /// <summary>Orden de la comida dentro del día.</summary>
    public int SortOrder { get; set; }

    /// <summary>FK opcional a MediaItem para imagen/video de la comida.</summary>
    public Guid? MediaId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public NutritionPlan Plan { get; set; } = default!;

    public MediaItem? Media { get; set; }
}
