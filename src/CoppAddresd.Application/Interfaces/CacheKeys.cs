using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Convención de claves de caché de la capa de aplicación. Formato
/// <c>&lt;dominio&gt;:&lt;nombre&gt;[:&lt;alcance&gt;]:v&lt;n&gt;</c> (el prefijo de
/// servicio lo añade la implementación). El sufijo de versión permite
/// invalidar globalmente un dominio subiendo la versión. Ver
/// docs/modules/cache/README.md para la tabla completa de claves.
/// </summary>
public static class CacheKeys
{
    /// <summary>Versión actual de todas las claves (bump para invalidar un dominio entero).</summary>
    public const string Version = "v1";

    /// <summary>TTL estándar de catálogos: lectura masiva, escritura rara.</summary>
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Rango de TTL de stats (segundos): jitter anti-stampede para que las
    /// claves no expiren sincronizadas (design D7 del change valkey-cache).
    /// </summary>
    public static readonly TimeSpan StatsTtlMin = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StatsTtlMax = TimeSpan.FromSeconds(60);

    /// <summary>Clave de un catálogo completo: <c>catalog:blood-types:v1</c>.</summary>
    public static string Catalog(string name) => $"catalog:{name}:{Version}";

    /// <summary>
    /// Clave de un catálogo particionado por un id (ej. estados por país):
    /// <c>catalog:states:{countryId}:v1</c>.
    /// </summary>
    public static string Catalog(string name, Guid scopeId) =>
        $"catalog:{name}:{scopeId}:{Version}";

    /// <summary>
    /// Clave de un agregado de stats, hashada por alcance (clínica, profesional
    /// propio, organización): dos usuarios con distinto alcance nunca comparten
    /// clave — el scoping lo resuelve el backend, nunca se filtra entre keys.
    /// </summary>
    public static string Stats(string name, string scopeHash) =>
        $"stats:{name}:{scopeHash}:{Version}";

    /// <summary>Hash estable del alcance para claves de stats (SHA-256, hex).</summary>
    public static string HashScope(params string?[] parts)
    {
        var raw = string.Join("|", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    /// <summary>TTL con jitter para stats dentro del rango configurado.</summary>
    public static TimeSpan StatsTtl() =>
        TimeSpan.FromSeconds(
            Random.Shared.Next((int)StatsTtlMin.TotalSeconds, (int)StatsTtlMax.TotalSeconds + 1)
        );
}
