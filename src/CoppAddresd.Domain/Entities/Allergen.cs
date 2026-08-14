namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Alergeno del catálogo de referencia del módulo de pacientes. Cada alergia
/// registrada (<see cref="PatientAllergy"/>) referencia una fila de este
/// catálogo; el nombre es único.
/// </summary>
public sealed class Allergen
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PatientAllergy> PatientAllergies { get; set; } = [];
}