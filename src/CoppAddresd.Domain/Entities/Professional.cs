namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Extensión clínica de un <see cref="Employee"/> (relación 1:0..1). Solo
/// existe para empleados con rol clínico (médicos, nutricionistas, psicólogos,
/// enfermería...). De aquí cuelgan especialidades, licencias y sedes de
/// atención. Un empleado no clínico simplemente no tiene fila aquí: agregar
/// futuros subtipos de empleado sigue el mismo patrón de extensión.
/// </summary>
public sealed class Professional
{
    public Guid Id { get; set; }

    /// <summary>Empleado núcleo (único: 1:0..1 con erp.employees).</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Profesión (catálogo). Puede completarse durante el onboarding.</summary>
    public Guid? ProfessionalTypeId { get; set; }

    /// <summary>Biografía profesional (la completa el profesional en onboarding).</summary>
    public string? Bio { get; set; }

    /// <summary>Clave del objeto de la foto en el storage (URL firmada al leer).</summary>
    public string? PhotoStorageKey { get; set; }

    /// <summary>Momento en que el profesional terminó el wizard de onboarding.</summary>
    public DateTime? OnboardingCompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Employee Employee { get; set; } = default!;

    public ProfessionalType? ProfessionalType { get; set; }

    public ICollection<ProfessionalLocation> LocationAssignments { get; set; } = [];

    public ICollection<ProfessionalSpecialty> Specialties { get; set; } = [];

    public ICollection<ProfessionalLicense> Licenses { get; set; } = [];
}
