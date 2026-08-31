namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Implementación nula (<c>Cache:Provider=None</c>): desactiva el caché por
/// completo — mecanismo de rollback del módulo sin tocar código.
/// </summary>
public sealed class NoCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

    public Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default
    )
        where T : class => factory(ct);
}
