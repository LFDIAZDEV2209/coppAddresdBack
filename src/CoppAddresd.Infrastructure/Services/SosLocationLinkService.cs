using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Capacidad de lectura limitada a una alerta: GUID binario + MAC de 128 bits.
/// Reutiliza la clave de firma de Storage con separación de propósito; nunca
/// incluye identidad, teléfono o coordenadas. No requiere otra clave secreta.
/// </summary>
public sealed class SosLocationLinkService : ISosLocationLinkService
{
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("coppadresd:sos-location:v1:");
    private readonly byte[]? key;
    private readonly Uri? publicBase;

    public SosLocationLinkService(IConfiguration configuration, IOptions<SosWebhookSettings> settings)
    {
        var secret = configuration["Storage:SignatureKey"];
        if (!string.IsNullOrWhiteSpace(secret) && Encoding.UTF8.GetByteCount(secret) >= 32)
        {
            key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Purpose);
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

    public string? CreateLink(SosAlert alert)
    {
        if (key is null || publicBase is null || alert.Id == Guid.Empty
            || alert.Latitude is null || alert.Longitude is null
            || !double.IsFinite(alert.Latitude.Value) || !double.IsFinite(alert.Longitude.Value)
            || Math.Abs(alert.Latitude.Value) > 90 || Math.Abs(alert.Longitude.Value) > 180)
        {
            return null;
        }
        var payload = new byte[32];
        alert.Id.TryWriteBytes(payload.AsSpan(0, 16));
        HMACSHA256.HashData(key, payload.AsSpan(0, 16)).AsSpan(0, 16).CopyTo(payload.AsSpan(16));
        var token = Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new Uri(publicBase, $"api/v1/sos/maps/{token}").AbsoluteUri;
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
