namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Relación N:N entre <see cref="ProfessionalType"/> (profesión) y
/// <see cref="Specialty"/> (especialidad). Define qué especialidades puede
/// ejercer cada profesión: catálogo de referencia que alimenta los filtros
/// de la UI y la validación de asignaciones.
/// </summary>
public sealed class ProfessionalTypeSpecialty
{
    public Guid ProfessionalTypeId { get; set; }

    public Guid SpecialtyId { get; set; }

    public ProfessionalType ProfessionalType { get; set; } = default!;

    public Specialty Specialty { get; set; } = default!;
}
