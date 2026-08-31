using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Credencial del service account de Google/Firebase (campos mínimos para el
/// flujo OAuth2 del FCM API v1). La clave privada se conserva en formato PEM.
/// </summary>
public sealed record ServiceAccountCredential(
    string ClientEmail,
    string PrivateKeyPem,
    string TokenUri);

/// <summary>
/// Helper de autenticación Google: parsea el service account (JSON crudo,
/// base64 o ruta a archivo) y genera el JWT firmado RS256 que el flujo OAuth2
/// (grant_type jwt-bearer) intercambia por un access token de Firebase.
/// </summary>
internal static class GoogleJwtHelper
{
    private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Resuelve el JSON del service account desde la configuración. Acepta:
    /// ruta a archivo existente, base64 del JSON o el JSON crudo.
    /// Devuelve null si no hay contenido o no es un service account válido.
    /// </summary>
    public static ServiceAccountCredential? ResolveServiceAccount(string? raw)
    {
        var json = ResolveJson(raw);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var clientEmail = GetString(root, "client_email");
            var privateKey = GetString(root, "private_key");
            var tokenUri = GetString(root, "token_uri");

            if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey))
                return null;

            return new ServiceAccountCredential(
                clientEmail,
                privateKey,
                string.IsNullOrWhiteSpace(tokenUri) ? DefaultTokenUri : tokenUri);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Genera el JWT (RS256) de la assertion OAuth2: header + claims (iss,
    /// scope, aud, iat, exp) firmados con la private key del service account.
    /// </summary>
    public static string CreateJwt(ServiceAccountCredential credential)
    {
        var now = DateTimeOffset.UtcNow;

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iss = credential.ClientEmail,
            scope = FcmScope,
            aud = credential.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        })));

        var unsigned = $"{header}.{payload}";
        var signature = Sign(credential.PrivateKeyPem, Encoding.UTF8.GetBytes(unsigned));
        return $"{unsigned}.{signature}";
    }

    /// <summary>
    /// Interpreta el valor de configuración: ruta de archivo, base64 o JSON
    /// crudo. Devuelve el JSON del service account, o null si no hay nada.
    /// </summary>
    private static string? ResolveJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();

        // Ruta a archivo con el JSON del service account.
        if (File.Exists(trimmed))
            return File.ReadAllText(trimmed);

        // Base64 del JSON (común en variables de entorno / secret managers).
        if (!trimmed.StartsWith('{'))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
                if (decoded.TrimStart().StartsWith('{'))
                    return decoded;
            }
            catch (FormatException)
            {
                // No era base64: se trata como JSON crudo.
            }
        }

        return trimmed;
    }

    private static string Sign(string privateKeyPem, byte[] data)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Base64UrlEncode(signature);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
}