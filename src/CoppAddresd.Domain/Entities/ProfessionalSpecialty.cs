namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación N:N de un <see cref="Professional"/> a una <see cref="Specialty"/>.
/// El catálogo <see cref="ProfessionalTypeSpecialty"/> define qué especialidades
/// son válidas para su profesión; esta asignación es la instancia concreta.
/// <c>IsPrimary</c> marca la especialidad principal del profesional.
/// </summary>
public sealed class ProfessionalSpecialty
{
    public Guid ProfessionalId { get; set; }

    public Guid SpecialtyId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Professional Professional { get; set; } = default!;

    public Specialty Specialty { get; set; } = default!;
}
