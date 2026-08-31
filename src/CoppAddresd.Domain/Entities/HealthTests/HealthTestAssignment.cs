using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Asignación individual de una versión de test a un paciente. Es la unidad de
/// trabajo del ERP ("pacientes pendientes por test") y del mobile ("mis tests").
/// El <c>BatteryAssignmentId</c> opcional la liga a una batería asignada.
/// </summary>
public sealed class HealthTestAssignment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Asignación de batería padre, si esta asignación proviene de una batería.</summary>
    public Guid? BatteryAssignmentId { get; set; }

    public Guid VersionId { get; set; }

    public HealthTestAssignmentStatus Status { get; set; } = HealthTestAssignmentStatus.pending;

    /// <summary>Prioridad de intervención (ej: 1..5, mayor = más urgente).</summary>
    public int? Priority { get; set; }

    /// <summary>Usuario (auth.users) que asignó; siempre derivado del JWT.</summary>
    public Guid? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? DueDate { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public PatientProfile? Patient { get; set; }

    public HealthTestBatteryAssignment? BatteryAssignment { get; set; }

    public HealthTestVersion? Version { get; set; }

    public ICollection<HealthTestEvaluation> Evaluations { get; set; } = [];
}
