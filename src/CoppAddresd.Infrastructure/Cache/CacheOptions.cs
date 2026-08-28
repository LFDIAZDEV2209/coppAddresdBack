namespace CoppAddresd.Infrastructure.Cache;

/// <summary>
/// Opciones del módulo de caché distribuida (sección <c>Cache</c>).
/// El proveedor se selecciona por entorno sin cambios de código: Valkey
/// (default, local o ElastiCache), Memory (tests/proceso único) o None
/// (rollback: todo fluye directo a PostgreSQL).
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public const string DefaultProvider = "Valkey";
    public const string DefaultKeyPrefix = "erp";

    /// <summary>Proveedor activo: <c>Valkey</c> | <c>Memory</c> | <c>None</c>.</summary>
    public string Provider { get; set; } = DefaultProvider;

    /// <summary>
    /// Namespace de claves por servicio (primera parte de cada clave):
    /// <c>erp</c> en el backend principal, <c>auth</c>/<c>tele</c> en los micros.
    /// </summary>
    public string KeyPrefix { get; set; } = "erp";
}
