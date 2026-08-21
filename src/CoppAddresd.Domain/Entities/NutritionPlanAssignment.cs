using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación de un plan de alimentación a un paciente.
/// Entidad separada de RoutineAssignment porque el dominio es distinto:
/// un plan alimentario tiene tracking de adherencia, calorías, y metas
/// que una rutina de ejercicio no tiene.
/// </summary>
public sealed class NutritionPlanAssignment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid PlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;

    /// <summary>Notas del profesional sobre la asignación.</summary>
    public string? Notes { get; set; }

    /// <summary>Id del usuario que creó la asignación (profesional).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public PatientProfile Patient { get; set; } = default!;

    public NutritionPlan Plan { get; set; } = default!;
}
