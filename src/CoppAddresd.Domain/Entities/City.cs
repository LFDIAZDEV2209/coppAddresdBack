namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Ciudad del catálogo geográfico, dependiente de un estado. Para catálogos
/// grandes la selección se hace por búsqueda/autocomplete (índice trigram).
/// </summary>
public sealed class City
{
    public Guid Id { get; set; }

    public Guid StateId { get; set; }

    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public State? State { get; set; }

    public ICollection<PostalCode> PostalCodes { get; set; } = [];
}