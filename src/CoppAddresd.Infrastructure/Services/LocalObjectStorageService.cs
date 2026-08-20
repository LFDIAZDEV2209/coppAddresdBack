using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IObjectStorageService"/> sobre el sistema de
/// archivos local. Cada objeto es un archivo: la clave <c>clientes/123/documento.pdf</c>
/// se resuelve a <c>{RootPath}/clientes/123/documento.pdf</c>.
/// </summary>
/// <remarks>
/// Es segura para singleton: sin estado mutable compartido (solo el directorio raíz,
/// inmutable) y con operaciones de archivo por llamada. <see cref="Directory.CreateDirectory"/>
/// es idempotente y thread-safe, por lo que no se requiere sincronización adicional.
/// </remarks>
public sealed class LocalObjectStorageService : IObjectStorageService
{
    private const int BufferSize = 81920;

    private readonly string _rootPath;

    public LocalObjectStorageService(IOptions<LocalStorageOptions> options)
    {
        // `Path.GetFullPath("")` lanza; RootPath vacío se resuelve al directorio
        // de trabajo (comportamiento de desarrollo sin configurar).
        var root = string.IsNullOrWhiteSpace(options.Value.RootPath) ? "." : options.Value.RootPath;
        _rootPath = Path.GetFullPath(root);
    }

    public async Task<string> PutObjectAsync(
        string key,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(key);

        // El contentType no se persiste en el sistema de archivos: se deriva
        // de la extensión al leer los metadatos (ver GetContentType).
        _ = contentType;

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        // Escritura atómica (semántica de PutObject de S3): primero se copia el contenido
        // a un archivo temporal en el MISMO directorio (mismo volumen => rename atómico) y
        // luego File.Move lo coloca sobre el destino. Si la copia falla a mitad de camino,
        // la versión previa queda intacta y el temporal se elimina.
        var tempPath = Path.Combine(directory, Path.GetFileName(fullPath) + ".tmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var tempStream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                await content.CopyToAsync(tempStream, ct);
                await tempStream.FlushAsync(ct);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // El temporal no existe o no pudo eliminarse: no debe enmascarar la excepción original.
            }

            throw;
        }

        return fullPath;
    }

    public Task<Stream> GetObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(key);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"El objeto '{key}' no existe en el almacenamiento local.", fullPath);

        return Task.FromResult<Stream>(new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true));
    }

    public Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(key);
        var fileInfo = new FileInfo(fullPath);

        if (!fileInfo.Exists)
            return Task.FromResult<ObjectMetadata?>(null);

        var lastModified = fileInfo.LastWriteTimeUtc;

        var metadata = new ObjectMetadata(
            Key: NormalizeKey(key),
            Size: fileInfo.Length,
            ETag: ComputeEtag(lastModified),
            ContentType: GetContentType(fullPath),
            LastModified: lastModified);

        return Task.FromResult<ObjectMetadata?>(metadata);
    }

    public Task<ListObjectsResult> ListObjectsAsync(
        string prefix,
        string? continuationToken = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        prefix ??= string.Empty;
        prefix = prefix.Replace('\\', '/');

        if (!Directory.Exists(_rootPath))
            return Task.FromResult(new ListObjectsResult(Array.Empty<ObjectMetadata>(), null));

        var keys = Directory
            .EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            // Defensa extra: se descartan rutas que atraviesen reparse points bajo la
            // raíz (symlinks/junctions), para que la enumeración jamás filtre objetos
            // ubicados fuera del directorio configurado.
            .Where(filePath => !HasReparsePointInPath(filePath))
            .Select(filePath => Path.GetRelativePath(_rootPath, filePath).Replace('\\', '/'))
            .Where(relative => relative.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();

        // Continuación simple: si se provee un token, se omiten las claves <= token.
        var skip = 0;
        if (continuationToken is not null)
            skip = keys.Count(key => string.CompareOrdinal(key, continuationToken) <= 0);

        var page = keys.Skip(skip).ToList();

        var items = page
            .Select(key => ToMetadata(key))
            .ToList();

        // Sin límite de página la lista devuelta es siempre completa, por lo que el
        // token siguiente solo tendría sentido si quedaran ítems sin devolver (p. ej.
        // si a futuro se agrega un límite estilo S3 de 1000 ítems por página).
        string? nextContinuationToken = skip + page.Count < keys.Count ? page[^1] : null;

        return Task.FromResult(new ListObjectsResult(items, nextContinuationToken));
    }

    public Task DeleteObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(key);

        // File.Delete no lanza si el archivo no existe: no-op natural.
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ct.ThrowIfCancellationRequested();

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fullPath = ResolvePath(key);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (FileNotFoundException)
            {
                // Eliminar un objeto inexistente es un no-op (semántica de lote S3).
            }
        }

        return Task.CompletedTask;
    }

    public Task CopyObjectAsync(
        string sourceKey,
        string destinationKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sourcePath = ResolvePath(sourceKey);
        var destinationPath = ResolvePath(destinationKey);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"El objeto origen '{sourceKey}' no existe en el almacenamiento local.", sourcePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        // overwrite: true reproduce la semántica de CopyObject de S3.
        File.Copy(sourcePath, destinationPath, overwrite: true);

        return Task.CompletedTask;
    }

    public Task<string> GetPreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // La implementación local no genera URLs firmadas: devuelve la ruta absoluta
        // del archivo. El parámetro expiry no tiene efecto aquí; existe solo para
        // mantener el contrato compatible con el proveedor AWS.
        _ = expiry;

        return Task.FromResult(ResolvePath(key));
    }

    /// <summary>
    /// Resuelve la clave del objeto a una ruta absoluta dentro de la raíz del
    /// almacenamiento. Rechaza claves vacías, path traversal (claves que escapen
    /// del directorio raíz) y, en Windows, nombres de dispositivo reservados de
    /// NTFS y flujos de datos alternativos. También rechaza rutas que atraviesen
    /// reparse points (symlinks/junctions) por debajo de la raíz.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Si la clave es vacía, intenta escapar de la raíz, o (en Windows) usa un nombre de
    /// dispositivo reservado o contiene ':' (flujo de datos alternativo).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Si la ruta resuelta atraviesa un reparse point que podría escapar de la raíz.
    /// </exception>
    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La clave del objeto no puede ser nula ni vacía.", nameof(key));

        if (OperatingSystem.IsWindows())
            ValidateWindowsKey(key);

        var normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));

        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"La clave '{key}' intenta escapar del directorio raíz del almacenamiento.", nameof(key));

        EnsureContained(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Nombres de dispositivo reservados del namespace DOS/NTFS de Windows. Una clave
    /// cuyo segmento coincida (con o sin extensión) con cualquiera de estos nombres
    /// resuelve al dispositivo y NO a un archivo dentro de la raíz, lo que rompería la
    /// guardia de contención (p. ej. <c>PutObjectAsync("NUL", ...)</c> no persiste nada).
    /// </summary>
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Valida una clave contra las restricciones del sistema de archivos NTFS:
    /// rechaza cualquier segmento que sea un nombre de dispositivo reservado (con o sin
    /// extensión) o que contenga ':' (flujo de datos alternativo). Solo aplica en Windows;
    /// en otros sistemas esos nombres son archivos legítimos.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Si algún segmento es un nombre de dispositivo reservado o contiene ':'.
    /// </exception>
    private static void ValidateWindowsKey(string key)
    {
        foreach (var segment in key.Split(['/', '\\']))
        {
            if (segment.Contains(':'))
                throw new ArgumentException(
                    $"La clave '{key}' contiene ':' (flujo de datos alternativo de NTFS) y no es válida.", nameof(key));

            var baseName = segment.Contains('.') ? segment[..segment.IndexOf('.')] : segment;
            if (ReservedDeviceNames.Contains(baseName))
                throw new ArgumentException(
                    $"La clave '{key}' usa el nombre de dispositivo reservado '{baseName}' y no es válida.", nameof(key));
        }
    }

    /// <summary>
    /// Verifica que la ruta resuelta no atraviese ningún reparse point (symlink o
    /// junction) por debajo de la raíz. <see cref="Path.GetFullPath"/> es léxico: no
    /// resuelve symlinks, pero las operaciones de archivo sí los siguen. Un junction bajo
    /// la raíz permitiría leer, escribir o borrar fuera del directorio configurado.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si algún componente existente de la ruta es un reparse point.
    /// </exception>
    private void EnsureContained(string fullPath)
    {
        if (HasReparsePointInPath(fullPath))
            throw new InvalidOperationException(
                $"La ruta '{fullPath}' atraviesa un reparse point bajo la raíz del almacenamiento y fue rechazada.");
    }

    /// <summary>
    /// Determina si algún componente existente de la ruta (por debajo de la raíz) tiene el
    /// atributo <see cref="FileAttributes.ReparsePoint"/>. Los componentes inexistentes se
    /// ignoran: no pueden ser reparse points. La raíz misma queda excluida (es el límite
    /// configurado del almacenamiento).
    /// </summary>
    private bool HasReparsePointInPath(string fullPath)
    {
        var relative = Path.GetRelativePath(_rootPath, fullPath);
        if (relative is "." or "")
            return false;

        var current = _rootPath;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);

            if (!File.Exists(current) && !Directory.Exists(current))
                return false;

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }

        return false;
    }

    /// <summary>Normaliza la clave a separador '/' (convención S3).</summary>
    private static string NormalizeKey(string key) => key.Replace('\\', '/');

    private ObjectMetadata ToMetadata(string key)
    {
        var fullPath = Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));
        var fileInfo = new FileInfo(fullPath);

        return new ObjectMetadata(
            Key: key,
            Size: fileInfo.Length,
            ETag: ComputeEtag(fileInfo.LastWriteTimeUtc),
            ContentType: GetContentType(fullPath),
            LastModified: fileInfo.LastWriteTimeUtc);
    }

    /// <summary>
    /// ETag estable por versión: hash del instante de última escritura.
    /// Cambia cuando el objeto se sobrescribe, sin depender de todo el contenido.
    /// </summary>
    private static string ComputeEtag(DateTime lastWriteTimeUtc)
    {
        var ticks = lastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ticks));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>
    /// Deriva el ContentType de la extensión del archivo (el sistema de archivos
    /// no persiste el contentType recibido en <see cref="PutObjectAsync"/>).
    /// </summary>
    private static string GetContentType(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }
}
