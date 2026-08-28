using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Implementación de <see cref="ICacheService"/> sobre <c>IMemoryCache</c>
/// (por instancia) para <c>Cache:Provider=Memory</c>: tests y despliegues de
/// una sola instancia.
/// </summary>
public sealed class MemoryCacheService(
    IMemoryCache cache,
    string keyPrefix,
    ILogger<MemoryCacheService> logger
) : ICacheService
{
    private string FullKey(string key) => $"{keyPrefix}:{key}";

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        var value = cache.TryGetValue(FullKey(key), out T? typed) ? typed : null;
        logger.LogDebug(value is not null ? "Cache HIT {Key}" : "Cache MISS {Key}", FullKey(key));
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        cache.Set(FullKey(key), value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        cache.Remove(FullKey(key));
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default
    )
        where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(ct);
        if (value is not null)
        {
            await SetAsync(key, value, ttl, ct);
        }

        return value;
    }
}
