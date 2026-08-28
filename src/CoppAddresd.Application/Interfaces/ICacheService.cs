namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Abstracción única de caché distribuida para los handlers de aplicación
/// (cache-aside). Toda clave escrita lleva TTL obligatorio (no existen claves
/// sin expiración) y las implementaciones degradan de forma controlada: un
/// fallo del backend de caché NUNCA rompe la request (fail-open a la fuente
/// de datos, log Warning). Ver docs/modules/cache/README.md.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Lee un valor tipado. Devuelve <c>null</c> si la clave no existe (miss),
    /// si expiró o si el caché no está disponible (fail-open).
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class;

    /// <summary>Escribe un valor con TTL obligatorio. Best-effort: un fallo se registra y se ignora.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Invalida una clave (llamar en el mismo flujo de la escritura que cambia
    /// el dato subyacente). Best-effort: un fallo se registra y se ignora.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Cache-aside: devuelve el valor cacheado y, en miss, lo construye con
    /// <paramref name="factory"/>, lo guarda con el TTL dado y lo devuelve.
    /// Bajo concurrencia dos misses pueden ejecutar la factory a la vez
    /// (sin lock distribuido por diseño: la reconstrucción es idempotente y
    /// barata — ver design.md D7 del change valkey-distributed-cache).
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default
    )
        where T : class;
}
