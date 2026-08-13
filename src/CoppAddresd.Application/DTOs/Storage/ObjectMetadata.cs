namespace CoppAddresd.Application.DTOs.Storage;

/// <summary>
/// Metadatos de un objeto almacenado, con la misma forma que la respuesta
/// <c>HeadObject</c> de S3.
/// </summary>
public record ObjectMetadata(
    string Key,
    long Size,
    string? ETag,
    string? ContentType,
    DateTimeOffset? LastModified)
{
    /// <summary>Etiquetas adicionales del objeto (por defecto, vacío).</summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}
