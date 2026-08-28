namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Opciones del módulo de caché de Telemedicina (sección <c>Cache</c>).
/// Provider: Valkey (default, local o ElastiCache), Memory (tests) o None
/// (rollback: sin caché, todo a la fuente).
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public const string DefaultProvider = "Valkey";
    public const string DefaultKeyPrefix = "tele";

    /// <summary>Proveedor activo: <c>Valkey</c> | <c>Memory</c> | <c>None</c>.</summary>
    public string Provider { get; set; } = DefaultProvider;

    /// <summary>Namespace de claves por servicio (primera parte de cada clave).</summary>
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;
}
