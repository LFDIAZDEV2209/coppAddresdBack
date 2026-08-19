namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Clínica bajo una <see cref="Organization"/>. Es la frontera principal del
/// modelo de permisos por contexto: un empleado se asigna a una clínica y sus
/// permisos pueden diferir entre clínicas. Una clínica tiene N sedes.
/// </summary>
public sealed class Clinic
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>Código opcional para integraciones (ej. facturación).</summary>
    public string? Code { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;

    public ICollection<Location> Locations { get; set; } = [];

    public ICollection<EmployeeClinic> EmployeeClinics { get; set; } = [];
}
