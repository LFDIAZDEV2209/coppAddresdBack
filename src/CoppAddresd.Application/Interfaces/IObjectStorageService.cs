using CoppAddresd.Application.DTOs.Storage;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Abstracción de almacenamiento de objetos (semántica S3) agnóstica del proveedor.
/// La implementación concreta (sistema de archivos local, AWS S3, etc.) se resuelve
/// en Infrastructure según la configuración <c>Storage:Provider</c>.
/// </summary>
/// <remarks>
/// Las claves usan separador <c>/</c> (convención S3), son relativas al bucket/raíz
/// y se interpretan como rutas: <c>clientes/123/documento.pdf</c>. El multipart upload
/// queda deliberadamente fuera del contrato: es un detalle de implementación que la
/// TransferUtility del SDK de AWS maneja internamente.
/// </remarks>
public interface IObjectStorageService
{
    /// <summary>
    /// True si el proveedor es almacenamiento en la nube (S3): genera URLs
    /// firmadas reales y el navegador sube/descarga directo al proveedor.
    /// False si usa el proxy del backend (proveedor Local).
    /// </summary>
    bool IsCloudStorage { get; }

    /// <summary>
    /// Almacena el contenido bajo la clave indicada (sobrescribe si ya existe) y
    /// devuelve la clave/resolución final del objeto.
    /// </summary>
    Task<string> PutObjectAsync(string key, Stream content, string? contentType = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el contenido del objeto. Lanza <see cref="FileNotFoundException"/> si la clave no existe.
    /// </summary>
    Task<Stream> GetObjectAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los metadatos del objeto, o <c>null</c> si la clave no existe.
    /// </summary>
    Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Lista los objetos cuya clave comienza con el prefijo, ordenados por clave.
    /// El token de continuación permite retomar la lista desde la última clave devuelta.
    /// </summary>
    Task<ListObjectsResult> ListObjectsAsync(string prefix, string? continuationToken = null, CancellationToken ct = default);

    /// <summary>
    /// Elimina el objeto. No-op si la clave no existe.
    /// </summary>
    Task DeleteObjectAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Elimina varios objetos en lote; tolera claves inexistentes sin lanzar.
    /// </summary>
    Task DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default);

    /// <summary>
    /// Copia el objeto origen sobre el destino (sobrescribe el destino si existe).
    /// Lanza <see cref="FileNotFoundException"/> si el origen no existe.
    /// </summary>
    Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct = default);

    /// <summary>
    /// Genera una URL firmada de acceso temporal al objeto.
    /// Semántica de la implementación local: devuelve la ruta absoluta del archivo
    /// (una URL real solo existe en el proveedor AWS S3).
    /// </summary>
    Task<string> GetPreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// Genera una URL firmada de escritura (PUT) para subir un objeto directo al
    /// proveedor. Con el proveedor Local devuelve la URL del proxy del backend
    /// (<c>{publicBaseUrl}/api/v1/storage/{{key}}</c>); con S3 devuelve un
    /// presigned URL real del bucket. <paramref name="publicBaseUrl"/> se ignora
    /// con S3 y solo se usa para construir el proxy local.
    /// </summary>
    Task<string> GetPreSignedUploadUrlAsync(
        string key,
        string? contentType,
        TimeSpan expiry,
        string publicBaseUrl,
        CancellationToken ct = default);
    // Métodos exclusivos del archivo nuevo: el llamador de lectura debe
    // autorizar paciente/clínica; la escritura/limpieza pertenece al handler.
    // Proveedores anteriores fallan cerrados hasta implementarlos explícitamente.
    Task<string> PutDocumentArchiveObjectAsync(string key, Stream content, string? contentType = null, CancellationToken ct = default)
        => Task.FromException<string>(new UnauthorizedAccessException("Archivo documental no habilitado en este proveedor."));
    Task<Stream> GetDocumentArchiveObjectAsync(string key, CancellationToken ct = default)
        => Task.FromException<Stream>(new UnauthorizedAccessException("Archivo documental no habilitado en este proveedor."));
    Task<string> GetDocumentArchiveReadUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => Task.FromException<string>(new UnauthorizedAccessException("Archivo documental no habilitado en este proveedor."));
    Task DeleteDocumentArchiveObjectAsync(string key, CancellationToken ct = default)
        => Task.FromException(new UnauthorizedAccessException("Archivo documental no habilitado en este proveedor."));

    // Exclusivos de fotos: el llamador comprueba permiso y paciente ANTES de usarlos.
    Task<Stream> GetFoodPhotoObjectAsync(string key, CancellationToken ct = default)
        => Task.FromException<Stream>(new UnauthorizedAccessException("Lectura de foto no habilitada en este proveedor."));
    Task<string> GetFoodPhotoReadUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
        => Task.FromException<string>(new UnauthorizedAccessException("Firma de foto no habilitada en este proveedor."));
}
