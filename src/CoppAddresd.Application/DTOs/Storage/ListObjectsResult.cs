namespace CoppAddresd.Application.DTOs.Storage;

/// <summary>
/// Resultado de una listado de objetos. <see cref="NextContinuationToken"/> es
/// <c>null</c> cuando no quedan más objetos por paginar.
/// </summary>
public record ListObjectsResult(
    IReadOnlyList<ObjectMetadata> Items,
    string? NextContinuationToken);
