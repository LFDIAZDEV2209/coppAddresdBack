namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Especialidad o área clínica (ej. Obesity Medicine, Clinical Nutrition).
/// Catálogo de referencia del schema <c>erp</c>. La categoría agrupa áreas
/// (Medicina, Nutrición, Salud mental, Enfermería, Terapia, Coordinación,
/// Fitness) para organizar la UI. Agregar especialidades no requiere cambios
/// de código: es dato del catálogo.
/// </summary>
public sealed class Specialty
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Área de agrupación (ej. "Medicina", "Nutrición").</summary>
    public string Category { get; set; } = default!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProfessionalSpecialty> ProfessionalSpecialties { get; set; } = [];

    public ICollection<ProfessionalTypeSpecialty> ProfessionalTypeSpecialties { get; set; } = [];
}
