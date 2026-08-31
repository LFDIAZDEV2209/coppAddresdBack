using CoppAddresd.Application.Interfaces;
using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Security;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Community.Storage;

/// <summary>
/// Info de subida de la imagen de una publicación: clave de storage, URL de
/// subida (PUT binario; presigned con S3 o proxy del servicio con Local) y URL
/// de lectura firmada (o presigned GET) para el <c>&lt;img&gt;</c>.
/// </summary>
public sealed record PostImageUploadInfo(string Key, string UploadUrl, string ReadUrl);

/// <summary>Resuelve los campos de imagen (avatarUrl, coverUrl) de los perfiles.</summary>
[ExtendObjectType<Profile>]
public sealed class ProfileImageUrlResolver
{
    private async Task<string?> ResolveAsync(
        string? storageKey,
        IObjectStorageService storage,
        IConfiguration config,
        StorageSignatureService signer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return null;

        if (storage.IsCloudStorage)
            return await storage.GetPreSignedUrlAsync(storageKey, TimeSpan.FromHours(1), ct);

        var publicBase = config["Storage:PublicBaseUrl"] ?? string.Empty;
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var signature = signer.Sign(storageKey, expiresAt);
        return
            $"{publicBase}/storage/{storageKey}?sig={signature}&exp={expiresAt.ToUnixTimeSeconds()}";
    }

    public Task<string?> GetAvatarUrlAsync(
        [Parent] Profile profile,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
        => ResolveAsync(profile.AvatarKey, storage, config, signer, ct);

    public Task<string?> GetCoverUrlAsync(
        [Parent] Profile profile,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
        => ResolveAsync(profile.CoverKey, storage, config, signer, ct);
}

/// <summary>Resuelve los campos <c>imageUrl</c> y <c>mediaType</c> de las publicaciones.</summary>
[ExtendObjectType<Post>]
public sealed class PostImageUrlResolver
{
    public async Task<string?> GetImageUrlAsync(
        [Parent] Post post,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(post.ImageKey))
            return null;

        if (storage.IsCloudStorage)
            return await storage.GetPreSignedUrlAsync(post.ImageKey, TimeSpan.FromHours(1), ct);

        var publicBase = config["Storage:PublicBaseUrl"] ?? string.Empty;
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var signature = signer.Sign(post.ImageKey, expiresAt);
        return
            $"{publicBase}/storage/{post.ImageKey}?sig={signature}&exp={expiresAt.ToUnixTimeSeconds()}";
    }

    /// <summary>Tipo del adjunto según la extensión de la clave (null si sin adjunto).</summary>
    public string? GetMediaType([Parent] Post post)
    {
        if (string.IsNullOrWhiteSpace(post.ImageKey))
            return null;

        var extension = System.IO.Path.GetExtension(post.ImageKey).ToLowerInvariant();
        return extension is ".mp4" or ".webm" ? "VIDEO" : "IMAGE";
    }
}
