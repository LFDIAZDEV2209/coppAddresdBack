namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Tipo de documento de identidad del catálogo administrativo, orientado al
/// contexto estadounidense (SSN, driver's license, pasaporte US, green card...).
/// </summary>
public sealed class DocumentType
{
    public Guid Id { get; set; }

    /// <summary>Código estable (SSN, DRIVERS_LICENSE, STATE_ID, ...).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}