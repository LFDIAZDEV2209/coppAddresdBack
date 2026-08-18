namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Estado/provincia/territorio del catálogo geográfico. Para Estados Unidos el
/// <see cref="Code"/> es el código ISO 3166-2 (ej. "CA", "FL", "DC").
/// </summary>
public sealed class State
{
    public Guid Id { get; set; }

    public Guid CountryId { get; set; }

    /// <summary>Código del estado (ISO 3166-2 para US, ej. "CA").</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Country? Country { get; set; }

    public ICollection<City> Cities { get; set; } = [];
}