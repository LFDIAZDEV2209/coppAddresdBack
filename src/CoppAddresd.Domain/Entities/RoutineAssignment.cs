using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación de una rutina de ejercicio a un paciente.
/// Vincula un template de rutina con un paciente, definiendo fechas y frecuencia.
/// </summary>
public sealed class RoutineAssignment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid RoutineId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public AssignmentFrequency Frequency { get; set; } = AssignmentFrequency.Diaria;

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;

    /// <summary>Notas del profesional sobre la asignación.</summary>
    public string? Notes { get; set; }

    /// <summary>Id del usuario que creó la asignación (profesional).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public PatientProfile Patient { get; set; } = default!;

    public ExerciseRoutine Routine { get; set; } = default!;
}
