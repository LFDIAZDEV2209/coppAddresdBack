namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación N:N de un <see cref="Professional"/> a una <see cref="Location"/>
/// (sedes donde atiende). Sin una fila explícita el profesional atiende en
/// todas las sedes de la clínica; con filas, solo en las asignadas.
/// </summary>
public sealed class ProfessionalLocation
{
    public Guid ProfessionalId { get; set; }

    public Guid LocationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Professional Professional { get; set; } = default!;

    public Location Location { get; set; } = default!;
}
