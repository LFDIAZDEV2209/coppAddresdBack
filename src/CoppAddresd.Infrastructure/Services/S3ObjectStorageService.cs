using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IObjectStorageService"/> sobre AWS S3
/// (SDK AWSSDK.S3). El binario vive en el bucket <c>Storage:S3:Bucket</c>
/// con claves relativas separadas por <c>/</c> (convención S3).
/// </summary>
/// <remarks>
/// - Credenciales: cadena por defecto del SDK (IAM role de EC2/ECS, perfil o
///   variables). Nunca Access Keys en código.
/// - Las URLs firmadas (GET/PUT) son generadas por S3; el proxy local
///   (<c>PUT/GET /api/v1/storage/{{key}}</c>) sigue funcionando para flujos
///   servidor-a-servidor, pero el navegador sube/descarga directo al bucket.
/// - Los errores de objeto inexistente se traducen a
///   <c>FileNotFoundException</c>/<c>null</c> para mantener el contrato de
///   <see cref="IObjectStorageService"/> independiente del proveedor.
/// </remarks>
public sealed class S3ObjectStorageService : IObjectStorageService
{
    private const int BatchDeleteMax = 1000;

    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3ObjectStorageService(IOptions<S3StorageOptions> options)
    {
        var settings = options.Value;
        settings.ApplyEnvironmentDefaults();

        if (string.IsNullOrWhiteSpace(settings.Bucket))
            throw new InvalidOperationException(
                "El proveedor de almacenamiento S3 exige configurar 'Storage:S3:Bucket' " +
                "(o la variable de entorno AWS_S3_BUCKET).");

        _bucket = settings.Bucket;

        var config = new AmazonS3Config
        {
            RegionEndpoint = ResolveRegion(settings.Region),
        };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            // Compatibilidad con LocalStack/MinIO en desarrollo.
            config.ServiceURL = settings.ServiceUrl;
            config.ForcePathStyle = true;
        }

        _client = new AmazonS3Client(config);
    }

    /// <summary>
    /// Constructor de prueba: permite inyectar un cliente S3 simulado sin tocar
    /// la cadena de credenciales de AWS. Solo para tests.
    /// </summary>
    internal S3ObjectStorageService(IAmazonS3 client, string bucket)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket;
    }

    public bool IsCloudStorage => true;

    public async Task<string> PutObjectAsync(
        string key,
        Stream content,
        string? contentType = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var response = await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType ?? GetContentType(key),
        }, ct);

        return response.ETag ?? key;
    }

    public async Task<Stream> GetObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var response = await _client.GetObjectAsync(
                new GetObjectRequest { BucketName = _bucket, Key = key }, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            throw new FileNotFoundException($"El objeto '{key}' no existe en el bucket S3.", key);
        }
    }

    public async Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var response = await _client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = _bucket, Key = key }, ct);

            return new ObjectMetadata(
                Key: key,
                Size: response.Headers?.ContentLength ?? 0,
                ETag: response.ETag,
                ContentType: response.Headers?.ContentType ?? GetContentType(key),
                LastModified: response.LastModified);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    public async Task<ListObjectsResult> ListObjectsAsync(
        string prefix,
        string? continuationToken = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        prefix ??= string.Empty;

        var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _bucket,
            Prefix = prefix,
            ContinuationToken = continuationToken,
        }, ct);

        var items = response.S3Objects
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new ObjectMetadata(
                Key: x.Key,
                Size: x.Size ?? 0,
                ETag: x.ETag?.Trim('"'),
                ContentType: GetContentType(x.Key),
                LastModified: x.LastModified))
            .ToList();

        return new ListObjectsResult(items, response.NextContinuationToken);
    }

    public async Task DeleteObjectAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // DeleteObject es idempotente: eliminar un objeto inexistente no es error.
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = key,
        }, ct);
    }

    public async Task DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ct.ThrowIfCancellationRequested();

        var keysList = keys.Distinct().ToList();
        if (keysList.Count == 0)
            return;

        // S3 permite como máximo 1000 objetos por DeleteObjects; se agrupan en lotes.
        foreach (var chunk in keysList.Chunk(BatchDeleteMax))
        {
            ct.ThrowIfCancellationRequested();

            await _client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = _bucket,
                Quiet = true,
                Objects = chunk.Select(key => new KeyVersion { Key = key }).ToList(),
            }, ct);
        }
    }

    public async Task CopyObjectAsync(
        string sourceKey,
        string destinationKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await _client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = _bucket,
                SourceKey = sourceKey,
                DestinationBucket = _bucket,
                DestinationKey = destinationKey,
            }, ct);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            throw new FileNotFoundException(
                $"El objeto origen '{sourceKey}' no existe en el bucket S3.", sourceKey);
        }
    }

    public Task<string> GetPreSignedUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
        });

        return Task.FromResult(url);
    }

    public Task<string> GetPreSignedUploadUrlAsync(
        string key,
        string? contentType,
        TimeSpan expiry,
        string publicBaseUrl,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ = publicBaseUrl; // El proveedor S3 genera la URL directamente, sin proxy.

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
        };

        // Solo se firma el Content-Type cuando el llamador lo conoce: el cliente
        // entonces DEBE enviarlo idéntico. Sin él, la URL acepta cualquier
        // Content-Type y S3 almacena el que el navegador envíe.
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            request.ContentType = contentType;
        }

        var url = _client.GetPreSignedURL(request);

        return Task.FromResult(url);
    }

    private static RegionEndpoint ResolveRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new InvalidOperationException(
                "El proveedor de almacenamiento S3 exige configurar 'Storage:S3:Region' " +
                "(o la variable de entorno AWS_REGION).");

        return RegionEndpoint.GetBySystemName(region);
    }

    private static bool IsNotFound(AmazonS3Exception ex)
        => ex.StatusCode == HttpStatusCode.NotFound;

    /// <summary>Deriva el Content-Type de la extensión cuando no se provee uno explícito.</summary>
    private static string GetContentType(string key)
    {
        var extension = Path.GetExtension(key).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".aac" => "audio/aac",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
    }
}