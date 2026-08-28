using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Asignación de una batería completa a un paciente (agrupación semántica para
/// la mobile y el onboarding). Las asignaciones individuales por test viven en
/// <see cref="HealthTestAssignment"/> con su <c>BatteryAssignmentId</c> opcional.
/// </summary>
public sealed class HealthTestBatteryAssignment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid BatteryId { get; set; }

    public HealthTestAssignmentStatus Status { get; set; } = HealthTestAssignmentStatus.pending;

    /// <summary>Usuario (auth.users) que asignó; siempre derivado del JWT.</summary>
    public Guid? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation
    public PatientProfile? Patient { get; set; }

    public HealthTestBattery? Battery { get; set; }

    public ICollection<HealthTestAssignment> Assignments { get; set; } = [];
}
