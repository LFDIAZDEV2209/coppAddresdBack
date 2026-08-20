namespace CoppAddresd.Domain.Entities;

/// <summary>Snapshot inmutable del contenido de un documento legal.</summary>
public sealed class LegalDocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int Major { get; set; }
    public int Minor { get; set; }
    public bool IsPublished { get; set; }
    public string Content { get; set; } = default!;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LegalDocument Document { get; set; } = default!;

    public string VersionLabel => $"{Major}.{Minor}";
}
