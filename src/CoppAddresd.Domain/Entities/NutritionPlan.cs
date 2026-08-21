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
