using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Firma URLs de lectura de objetos del storage (HMAC-SHA256). Permite que el
/// <c>&lt;video&gt;</c>/<c>&lt;audio&gt;</c> del navegador consuma el archivo sin
/// poder enviar el header <c>Authorization</c>: la URL lleva <c>sig</c> + <c>exp</c>
/// y el endpoint de descarga la valida. Es el equivalente local del presigned URL de S3.
/// </summary>
public sealed class StorageSignatureService
{
    private readonly byte[] _key;

    public StorageSignatureService(string signatureKey)
    {
        _key = Encoding.UTF8.GetBytes(signatureKey);
    }

    /// <summary>Firma la clave de storage con expiración. Devuelve sig (hex).</summary>
    public string Sign(string storageKey, DateTimeOffset expiresAt)
    {
        var payload = $"{storageKey}|{expiresAt.ToUnixTimeSeconds()}";
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Valida que la firma corresponda a la clave y no esté expirada.</summary>
    public bool Validate(string storageKey, string? signature, string? expiresAtRaw, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signature) || !long.TryParse(expiresAtRaw, out var expiresAtUnix))
            return false;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
        if (now > expiresAt)
            return false;

        var expected = Sign(storageKey, expiresAt);
        return FixedTimeEquals(expected, signature);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        if (left.Length != right.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < left.Length; i++)
            diff |= left[i] ^ right[i];
        return diff == 0;
    }
}
