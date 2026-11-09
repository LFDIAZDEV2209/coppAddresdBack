using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Capacidad corta de 128 bits con digest persistido; acepta también GUID + MAC legado.
/// Reutiliza la clave de firma de Storage con separación de propósito; nunca
/// incluye identidad, teléfono o coordenadas. No requiere otra clave secreta.
/// </summary>
public sealed class SosLocationLinkService : ISosLocationLinkService
{
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("coppadresd:sos-location:v1:");
    private static readonly byte[] ShortPurpose = Encoding.UTF8.GetBytes("coppadresd:sos-location:v2:");
    private readonly byte[]? key;
    private readonly byte[]? shortKey;
    private readonly Uri? publicBase;

    public SosLocationLinkService(IConfiguration configuration, IOptions<SosWebhookSettings> settings)
    {
        var secret = configuration["Storage:SignatureKey"];
        if (!string.IsNullOrWhiteSpace(secret) && Encoding.UTF8.GetByteCount(secret) >= 32)
        {
            key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Purpose);
            shortKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), ShortPurpose);
        }
        var originText = configuration["SosLocationLinks:PublicBaseUrl"] ?? settings.Value.NormalizedBaseUrl;
        if (Uri.TryCreate(originText, UriKind.Absolute, out var origin)
            && origin.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment) && origin.AbsolutePath == "/")
        {
            publicBase = origin;
        }
    }

    public void Prepare(SosAlert alert)
    {
        if (CanShare(alert)) alert.LocationLinkHash = Hash(ShortCode(alert.Id));
    }

    private bool CanShare(SosAlert alert) => key is not null && publicBase is not null && alert.Id != Guid.Empty
            && alert.Latitude is double latitude && alert.Longitude is double longitude
            && double.IsFinite(latitude) && double.IsFinite(longitude)
            && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180;

    private string ShortCode(Guid id) => Encode(HMACSHA256.HashData(shortKey!, id.ToByteArray()).AsSpan(0, 16));
    private static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    public bool TryGetShortCodeHash(string? token, out string hash)
    {
        hash = "";
        if (key is null || token is null || token.Length != 22
            || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) return false;
        hash = Hash(token);
        return true;
    }

    public string? CreateLink(SosAlert alert)
    {
        if (!CanShare(alert)) return null;
        var code = ShortCode(alert.Id);
        if (alert.LocationLinkHash == Hash(code))
            return new Uri(publicBase!, $"api/v1/s/{code}").AbsoluteUri;
        // Filas antiguas (o cambio de clave): no emitir una capacidad corta
        // cuyo digest no quedó en el mismo commit que la alerta.
        var payload = new byte[32];
        alert.Id.TryWriteBytes(payload.AsSpan(0, 16));
        HMACSHA256.HashData(key!, payload.AsSpan(0, 16)).AsSpan(0, 16).CopyTo(payload.AsSpan(16));
        return new Uri(publicBase!, $"api/v1/sos/maps/{Encode(payload)}").AbsoluteUri;
    }

    public bool TryReadToken(string? token, out Guid alertId)
    {
        alertId = Guid.Empty;
        if (key is null || token is null || token.Length != 43
            || token.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
        {
            return false;
        }
        Span<byte> payload = stackalloc byte[32];
        var encoded = token.Replace('-', '+').Replace('_', '/') + "=";
        if (!Convert.TryFromBase64String(encoded, payload, out var written) || written != payload.Length)
        {
            return false;
        }
        var expected = HMACSHA256.HashData(key, payload[..16]);
        if (!CryptographicOperations.FixedTimeEquals(expected.AsSpan(0, 16), payload[16..]))
        {
            return false;
        }
        alertId = new Guid(payload[..16]);
        return alertId != Guid.Empty;
    }
}
