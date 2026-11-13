using System.Text.RegularExpressions;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Enlaces de video admitidos; solo se valida la URL, sin descargar contenido.</summary>
public static partial class ExternalMediaRules
{
    public static string NormalizeSource(string? source) => source?.Trim().ToLowerInvariant() ?? "file";

    public static bool TryParseVideo(string? url, out string? provider, out string? videoId)
    {
        provider = null; videoId = null;
        if (!IsHttpsUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        string? id = null;
        if (host == "youtu.be")
        {
            if (parts.Length == 1) id = parts[0];
            provider = "youtube";
        }
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "www.youtube-nocookie.com")
        {
            provider = "youtube";
            if (parts.Length == 2 && parts[0] is "embed" or "shorts") id = parts[1];
            else if (parts.Length == 1 && parts[0] == "watch")
            {
                var values = uri.Query.TrimStart('?').Split('&')
                    .Select(value => value.Split('=', 2)).Where(value => value.Length == 2 && value[0] == "v").ToArray();
                if (values.Length == 1) id = Uri.UnescapeDataString(values[0][1]);
            }
        }
        else if (host is "vimeo.com" or "www.vimeo.com")
        {
            provider = "vimeo";
            if (parts.Length == 1) id = parts[0];
        }
        else if (host == "player.vimeo.com")
        {
            provider = "vimeo";
            if (parts.Length == 2 && parts[0] == "video") id = parts[1];
        }
        if (id is null || !(provider == "youtube" ? YoutubeId().IsMatch(id) : provider == "vimeo" && VimeoId().IsMatch(id)))
        { provider = null; return false; }
        videoId = id;
        return true;
    }

    public static string? ProviderFor(string? url) => TryParseVideo(url, out var provider, out _) ? provider : null;

    public static bool IsHttpsUrl(string? url) => url is { Length: <= 2048 }
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    public static bool IsValid(string? source, string? provider, string? url, MediaType type)
    {
        source = NormalizeSource(source);
        if (source == "file") return string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(url);
        return source == "external" && type == MediaType.Video
            && TryParseVideo(url, out var actualProvider, out _)
            && string.Equals(provider?.Trim(), actualProvider, StringComparison.OrdinalIgnoreCase);
    }

    public static string StorageKey(string? source, string? provider, string? url, MediaType type, string? fileKey)
    {
        if (!IsValid(source, provider, url, type))
            throw new UnprocessableEntityException("EXTERNAL_MEDIA_INVALID: origen, proveedor o URL de video inválidos.");
        if (NormalizeSource(source) == "file")
            return !string.IsNullOrWhiteSpace(fileKey) ? fileKey : throw new UnprocessableEntityException("La clave de storage es requerida.");
        TryParseVideo(url, out var parsedProvider, out var id);
        return $"external/{parsedProvider}/{id}";
    }

    public static string? ResolveThumbnail(string? key, string? url)
    {
        if (url is null) return key;
        if (!IsHttpsUrl(url) || url.Length > 1024)
            throw new UnprocessableEntityException("La URL de portada debe ser HTTPS y válida.");
        return url;
    }

    public static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (!ValidTags(tags)) throw new UnprocessableEntityException("Máximo 32 etiquetas, de 1 a 64 caracteres.");
        return tags?.Select(t => t.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order().ToArray() ?? [];
    }

    public static bool ValidTags(IReadOnlyList<string>? tags) => tags is null ||
        tags.Count <= 32 && tags.All(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Length <= 64);

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex YoutubeId();
    [GeneratedRegex("^[1-9][0-9]{0,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex VimeoId();
}
