namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Etnia del catálogo demográfico. Los valores siguen las categorías OMB
/// (SPD 15, revisión 2024: combined race/ethnicity categories) usadas por los
/// sistemas administrativos de salud en Estados Unidos.
/// </summary>
public sealed class Ethnicity
{
    public Guid Id { get; set; }

    /// <summary>Código estable (ej. "HISPANIC_OR_LATINO").</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}