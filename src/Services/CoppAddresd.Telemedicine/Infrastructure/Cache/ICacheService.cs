namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Abstracción única de caché distribuida del microservicio de Telemedicina
/// (contrato idéntico al de backend/Auth: el micro es standalone y no
/// referencia proyectos externos). Toda clave lleva TTL obligatorio y las
/// implementaciones degradan de forma controlada: un fallo de Valkey NUNCA
/// rompe una petición (fail-open, log Warning). Ver docs/modules/cache/README.md.
/// </summary>
public interface ICacheService
{
    /// <summary>Lee un valor tipado; <c>null</c> si miss, expiró o el caché está degradado.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class;

    /// <summary>Escribe un valor con TTL obligatorio. Best-effort.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class;

    /// <summary>Invalida una clave. Best-effort.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Cache-aside: devuelve el valor cacheado y, en miss, lo construye con
    /// <paramref name="factory"/> y lo guarda con el TTL dado.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default
    )
        where T : class;
}
