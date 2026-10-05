using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Auth.Security;

/// <summary>
/// Secretos del flujo de eliminación de cuenta (código de entrega app → web y
/// sesión de la web): 256 bits aleatorios; en BD solo se guarda su SHA-256.
/// </summary>
public static class AccountDeletionSecrets
{
    public static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
}
