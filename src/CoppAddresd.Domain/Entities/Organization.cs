namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Organización (proveedor raíz del tenant, ej. MediQuer). Es el nivel superior
/// del modelo multi-organización: una organización tiene N clínicas y N
/// profesionales. Los scopes de autorización cuelgan de aquí hacia abajo
/// (Organization → Clinic → Location).
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; }

    /// <summary>Código estable interno (ej. "mediquer"). Único.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Clinic> Clinics { get; set; } = [];

    public ICollection<Employee> Employees { get; set; } = [];
}
