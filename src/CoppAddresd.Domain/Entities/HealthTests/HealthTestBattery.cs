namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Batería de evaluación: conjunto configurable de instrumentos con orden,
/// obligatoriedad y periodicidad. Las baterías pueden crearse, activarse y
/// desactivarse sin afectar evaluaciones existentes (SPEC A4).
/// </summary>
public sealed class HealthTestBattery
{
    public Guid Id { get; set; }

    /// <summary>Código único de negocio (ej: "bateria-inicial").</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Auto-asigna esta batería a cada paciente recién creado (SPEC A7).</summary>
    public bool AutoAssignOnPatientCreate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<HealthTestBatteryItem> Items { get; set; } = [];

    public ICollection<HealthTestBatteryAssignment> Assignments { get; set; } = [];
}
