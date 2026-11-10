namespace CoppAddresd.Domain.Entities;

/// <summary>Receipt for the exact published document and approved processing disclosure shown to a user.</summary>
public sealed class LegalDocumentAcceptance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LegalDocumentVersionId { get; set; }
    public string Scope { get; set; } = default!;
    public string Locale { get; set; } = default!;
    public string DisclosureId { get; set; } = default!;
    public string DisclosureSnapshot { get; set; } = default!;
    public Guid IdempotencyKey { get; set; }
    public DateTime AcceptedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}
