namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Código postal del catálogo geográfico (US ZIP). Un código postal puede
/// servir a varias ciudades y una ciudad puede tener varios códigos; la
/// combinación ciudad + código postal es única.
/// </summary>
public sealed class PostalCode
{
    public Guid Id { get; set; }

    public Guid CityId { get; set; }

    public string ZipCode { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public City? City { get; set; }
}