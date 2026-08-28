using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Almacenamiento local de imágenes de comida sobre el sistema de archivos.
/// Delega en <see cref="IObjectStorageService"/> (patrón existente de
/// CoppAddresd): con Storage:Provider=S3 el mismo código persiste en S3 sin
/// cambios — el prefijo canónico es <c>foodai/</c>.
/// </summary>
public sealed class LocalImageStorage : IImageStorage
{
    private readonly IObjectStorageService _storage;

    public LocalImageStorage(IObjectStorageService storage)
    {
        _storage = storage;
    }

    public async Task<string> SaveImageAsync(
        Guid analysisId,
        string fileName,
        Stream content,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var key = $"foodai/{analysisId:N}{extension}";
        return await _storage.PutObjectAsync(key, content, contentType: null, ct);
    }

    public async Task<string> SaveMaskAsync(
        Guid analysisId,
        int itemIndex,
        Stream pngContent,
        CancellationToken ct = default)
    {
        var key = $"foodai/masks/{analysisId:N}/{itemIndex}.png";
        return await _storage.PutObjectAsync(key, pngContent, contentType: "image/png", ct);
    }
}