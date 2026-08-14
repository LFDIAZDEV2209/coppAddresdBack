namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Alergia registrada de un paciente. Una fila por alergeno del catálogo
/// <see cref="Entities.Allergen"/>; la combinación paciente + alergeno es única.
/// </summary>
public sealed class PatientAllergy
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Alergeno del catálogo de referencia (FK a <see cref="Entities.Allergen"/>).</summary>
    public Guid AllergenId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public Allergen? Allergen { get; set; }
}