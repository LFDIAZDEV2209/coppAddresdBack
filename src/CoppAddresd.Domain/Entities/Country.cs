namespace CoppAddresd.Domain.Entities;

/// <summary>
/// País del catálogo geográfico (ISO 3166-1 alpha-2). Incluye el código
/// telefónico E.164 (sin el prefijo '+') usado por el selector de teléfono.
/// </summary>
public sealed class Country
{
    public Guid Id { get; set; }

    /// <summary>Código ISO 3166-1 alpha-2 (ej. "US").</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Código telefónico E.164 sin '+' (ej. "1", "52").</summary>
    public string PhoneCode { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<State> States { get; set; } = [];
}