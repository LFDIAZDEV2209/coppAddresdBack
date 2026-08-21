namespace CoppAddresd.Domain.Entities;

/// <summary>Documento legal editable que consume la aplicación móvil.</summary>
public sealed class LegalDocument
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Title { get; set; } = default!;
    public Guid? CurrentVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<LegalDocumentVersion> Versions { get; set; } = [];
}
