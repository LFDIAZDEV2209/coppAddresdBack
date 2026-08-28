namespace CoppAddresd.Auth.Services.Cache;

/// <summary>
/// Opciones del módulo de caché del Auth Service (sección <c>Cache</c>).
/// Provider: Valkey (default, local o ElastiCache), Memory (tests) o None
/// (rollback: sin caché, todo a PostgreSQL).
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public const string DefaultProvider = "Valkey";
    public const string DefaultKeyPrefix = "auth";

    /// <summary>Proveedor activo: <c>Valkey</c> | <c>Memory</c> | <c>None</c>.</summary>
    public string Provider { get; set; } = DefaultProvider;

    /// <summary>Namespace de claves por servicio (primera parte de cada clave).</summary>
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;
}
