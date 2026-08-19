namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Tipo de profesional (profesión, ej. Physician, Registered Dietitian).
/// Catálogo de referencia del schema <c>erp</c>; distinto de la especialidad
/// (<see cref="Specialty"/>) y de la credencial (<see cref="ProfessionalLicense"/>).
/// El mapeo N:N con especialidades (<see cref="ProfessionalTypeSpecialty"/>)
/// define qué especialidades puede ejercer cada profesión y alimenta los
/// filtros de la UI.
/// </summary>
public sealed class ProfessionalType
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Professional> Professionals { get; set; } = [];

    public ICollection<ProfessionalTypeSpecialty> Specialties { get; set; } = [];
}
