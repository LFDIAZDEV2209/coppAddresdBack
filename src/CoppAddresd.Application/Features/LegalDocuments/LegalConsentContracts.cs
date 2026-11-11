namespace CoppAddresd.Application.Features.LegalDocuments;

public static class LegalConsentScopes
{
    public const string Program = "program";
    public const string Chat = "chat";
    public const string Voice = "voice";
    public const string Food = "food";
    public const string Lab = "lab";
    public const string PatientAi = "patient-ai";
    public const string Sos = "sos";
    public static readonly string[] All = [Program, Chat, Voice, Food, Lab, PatientAi, Sos];
}

/// <summary>No legal text or provider assumptions are supplied by default. Deployment must approve each scope.</summary>
public sealed class LegalConsentSettings
{
    public const string SectionName = "LegalConsents";
    // Activación separada de la disponibilidad de documentos; restauración de TestFlight.
    public bool EnforcementEnabled { get; set; }
    public Dictionary<string, LegalConsentScopeSettings> Scopes { get; set; } = [];
}

public sealed class LegalConsentScopeSettings
{
    public bool Approved { get; set; }
    public string ProcessingRevision { get; set; } = "";
    public string[] Providers { get; set; } = [];
    public Dictionary<string, string> Documents { get; set; } = [];
}

public sealed record LegalConsentDocumentDto(string Code, Guid VersionId, string Version,
    string Title, string Content, string Locale, DateTime PublishedAt);
public sealed record LegalConsentScopeDto(string Scope, bool Available, bool Accepted,
    string? DisclosureId, IReadOnlyList<string> Providers, IReadOnlyList<LegalConsentDocumentDto> Documents,
    Guid? ReceiptId = null, DateTime? AcceptedAt = null);
public sealed record LegalConsentStatusDto(string Locale, IReadOnlyList<LegalConsentScopeDto> Scopes);
public sealed record AcceptLegalConsentRequest(string Scope, Guid VersionId, string Locale,
    string DisclosureId, Guid IdempotencyKey);
public sealed record LegalConsentReceiptDto(Guid Id, string Scope, Guid VersionId, string Locale,
    string DisclosureId, DateTime AcceptedAt, DateTime? WithdrawnAt);
