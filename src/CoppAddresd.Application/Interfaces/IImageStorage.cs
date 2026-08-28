namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Almacenamiento de imágenes de análisis de comida. Abstracción preparada
/// para múltiples backends (Local → S3): la implementación actual delega en
/// <see cref="IObjectStorageService"/>, que ya soporta Local y S3 vía
/// configuración (Storage:Provider), con clave canónica <c>foodai/&lt;analysisId&gt;.&lt;ext&gt;</c>.
/// </summary>
public interface IImageStorage
{
    Task<string> SaveImageAsync(
        Guid analysisId,
        string fileName,
        Stream content,
        CancellationToken ct = default);

    /// <summary>
    /// Guarda la máscara de segmentación de un item (PNG) en object storage.
    /// La BD solo conserva la clave (<c>foodai/masks/&lt;analysisId&gt;/&lt;index&gt;.png</c>),
    /// nunca el base64.
    /// </summary>
    Task<string> SaveMaskAsync(
        Guid analysisId,
        int itemIndex,
        Stream pngContent,
        CancellationToken ct = default);
}