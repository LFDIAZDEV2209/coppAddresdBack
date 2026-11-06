namespace CoppAddresd.Domain.Entities;

/// <summary>Créditos inmutables de un recurso importado. Metadata original conserva
/// avisos, historial de autores y fuentes derivadas sin inventar titularidad.</summary>
public sealed record MediaProvenance(
    string Provider, string ExternalId, string Title, string Author,
    string? AuthorUrl, string SourceUrl, string LicenseName, string LicenseUrl,
    string Changes, string OriginalMediaUrl, string OriginalMetadata,
    DateTimeOffset RetrievedAt, string? DerivativeSourceUrl = null);
