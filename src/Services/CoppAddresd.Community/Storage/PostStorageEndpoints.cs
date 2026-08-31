using CoppAddresd.Application.Interfaces;
using CoppAddresd.Community.Security;

namespace CoppAddresd.Community.Storage;

/// <summary>
/// Endpoints REST de imágenes de publicaciones (sin tocar el API principal):
/// - <c>PUT /storage/community/posts/{**key}</c>: subida con Bearer (JWT de la app).
/// - <c>GET /storage/community/posts/{**key}</c>: lectura con URL firmada (sig+exp)
///   para que el <c>&lt;img&gt;</c> del navegador no necesite header Authorization.
/// Con el proveedor S3 el navegador sube/lee directo al bucket vía URLs firmadas
/// y estos endpoints quedan como respaldo local (proveedor Local).
/// </summary>
public static class PostStorageEndpoints
{
    /// <summary>Prefijos de clave reservados (publicaciones, avatares, portadas).</summary>
    public const string KeyPrefix = "community/posts/";
    public const string AvatarPrefix = "community/avatars/";
    public const string CoverPrefix = "community/covers/";

    /// <summary>Prefijos permitidos para los adjuntos del servicio de comunidad.</summary>
    private static readonly string[] AllowedPrefixes =
        [KeyPrefix, AvatarPrefix, CoverPrefix];

    /// <summary>Límite de tamaño por imagen (8 MB).</summary>
    private const long MaxImageBytes = 8 * 1024 * 1024;

    /// <summary>Límite de tamaño por video (100 MB).</summary>
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
            "image/heic",
            "image/heif",
            "video/mp4",
            "video/webm",
        };

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".heif",
            ".mp4", ".webm",
        };

    /// <summary>Valida el content-type del adjunto (imagen o video permitidos).</summary>
    public static bool IsAllowedImageContentType(string contentType)
        => AllowedContentTypes.Contains(contentType.Split(';')[0].Trim().ToLowerInvariant());

    /// <summary>Valida la extensión del adjunto (imagen o video permitidos).</summary>
    public static bool IsAllowedImageExtension(string extension) => AllowedExtensions.Contains(extension);

    private static bool IsVideoContentType(string contentType)
        => contentType.Split(';')[0].Trim().ToLowerInvariant() is "video/mp4" or "video/webm";

    public static IEndpointRouteBuilder MapPostStorageEndpoints(this IEndpointRouteBuilder app)
    {
        // Ojo: dentro de MapGroup el catch-all debe ser absoluto ("/{**key}"),
        // de lo contrario no captura los segmentos previos de la ruta del grupo.
        // El GET es público (su auth es la firma sig+exp, para que el <img> del
        // navegador no necesite el header Authorization); el PUT exige JWT.
        var group = app.MapGroup("/storage");

        group
            .MapPut("/{**key}", PutPostImageAsync)
            .RequireAuthorization()
            .WithName("PutPostImage")
            .DisableAntiforgery();

        group
            .MapGet("/{**key}", GetPostImageAsync)
            .WithName("GetPostImage");

        return app;
    }

    /// <summary>Valida una clave de imagen/publicación (prefijo reservado y seguro).</summary>
    public static bool IsValidPostImageKey(string key)
        => !string.IsNullOrWhiteSpace(key)
           && AllowedPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal))
           && !key.Contains("..", StringComparison.Ordinal)
           && !key.EndsWith('/')
           && key.Contains('/', StringComparison.Ordinal);

    /// <summary>True si la clave pertenece al espacio de avatares.</summary>
    public static bool IsAvatarKey(string key)
        => key.StartsWith(AvatarPrefix, StringComparison.Ordinal);

    /// <summary>True si la clave pertenece al espacio de portadas.</summary>
    public static bool IsCoverKey(string key)
        => key.StartsWith(CoverPrefix, StringComparison.Ordinal);

    private static async Task<IResult> PutPostImageAsync(
        string key,
        HttpRequest request,
        IObjectStorageService storage,
        CancellationToken ct)
    {
        if (!IsValidPostImageKey(key))
            return Results.BadRequest("Clave de imagen inválida.");

        var contentType = request.ContentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            return Results.BadRequest($"Tipo de adjunto no permitido: '{contentType}'.");

        var maxBytes = IsVideoContentType(contentType) ? MaxVideoBytes : MaxImageBytes;
        if (request.ContentLength > maxBytes)
            return Results.BadRequest($"El adjunto supera el límite de {maxBytes / (1024 * 1024)} MB.");

        await storage.PutObjectAsync(key, request.Body, contentType, ct);
        return Results.Ok(new { key });
    }

    private static async Task<IResult> GetPostImageAsync(
        string key,
        string? sig,
        long? exp,
        IObjectStorageService storage,
        StorageSignatureService signer,
        CancellationToken ct)
    {
        if (!IsValidPostImageKey(key))
            return Results.BadRequest("Clave de imagen inválida.");

        if (exp is null || !signer.Validate(key, sig, exp.Value, DateTimeOffset.UtcNow))
            return Results.Unauthorized();

        var meta = await storage.HeadObjectAsync(key, ct);
        if (meta is null)
            return Results.NotFound();

        var stream = await storage.GetObjectAsync(key, ct);
        return TypedResults.File(
            stream,
            meta.ContentType,
            enableRangeProcessing: true,
            lastModified: meta.LastModified,
            entityTag: string.IsNullOrWhiteSpace(meta.ETag)
                ? null
                : new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{meta.ETag.Trim('"')}\""));
    }
}
