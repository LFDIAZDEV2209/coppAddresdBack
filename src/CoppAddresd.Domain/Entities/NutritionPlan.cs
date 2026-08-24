using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Plan de alimentación. Puede ser un template genérico (<see cref="IsTemplate"/>=true)
/// o un plan personalizado asignado a un paciente específico. Los templates se clonan
/// para crear planes personalizados sin afectar el original.
/// </summary>
public sealed class NutritionPlan
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Condición objetivo: Obesidad, Diabetes, Mantenimiento, etc.</summary>
    public string? TargetCondition { get; set; }

    /// <summary>Duración total del plan en días.</summary>
    public int DurationDays { get; set; }

    /// <summary>Meta calórica diaria en kcal.</summary>
    public int? DailyCalorieTarget { get; set; }

    /// <summary>Gramos diarios objetivo de proteína.</summary>
    public decimal? DailyProteinTarget { get; set; }

    /// <summary>Gramos diarios objetivo de carbohidratos.</summary>
    public decimal? DailyCarbsTarget { get; set; }

    /// <summary>Gramos diarios objetivo de grasa.</summary>
    public decimal? DailyFatTarget { get; set; }

    /// <summary>Gramos diarios objetivo de fibra.</summary>
    public decimal? DailyFiberTarget { get; set; }

    /// <summary>Restricciones/alergias del paciente (gluten, lactosa, frutos secos, etc.).</summary>
    public string? Allergens { get; set; }

    /// <summary>Horarios preferidos de comida (ej: "7:00, 12:00, 15:30, 19:00").</summary>
    public string? MealTiming { get; set; }

    /// <summary>True = template de la biblioteca; False = plan personalizado de un paciente.</summary>
    public bool IsTemplate { get; set; } = true;

    /// <summary>Id del paciente dueño del plan. Null si es template.</summary>
    public Guid? PatientId { get; set; }

    /// <summary>Id del template original del que se clonó este plan. Null si es el template raíz.</summary>
    public Guid? SourcePlanId { get; set; }

    public NutritionPlanStatus Status { get; set; } = NutritionPlanStatus.Draft;

    /// <summary>Id del usuario que creó el plan (profesional).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public PatientProfile? Patient { get; set; }

    public NutritionPlan? SourcePlan { get; set; }

    public ICollection<NutritionPlanDay> Days { get; set; } = [];
}
