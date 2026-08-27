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
}