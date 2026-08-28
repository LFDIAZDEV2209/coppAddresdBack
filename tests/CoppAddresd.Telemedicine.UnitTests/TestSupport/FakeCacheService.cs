using System.Collections.Concurrent;
using CoppAddresd.Telemedicine.Infrastructure.Cache;

namespace CoppAddresd.Telemedicine.UnitTests.TestSupport;

/// <summary>
/// Doble de prueba de <see cref="ICacheService"/> de Telemedicina: diccionario
/// en memoria con la semántica del contrato y contadores hit/miss/set/remove.
/// </summary>
public sealed class FakeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object?> _store = new();

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public List<string> Removed { get; } = [];
    public List<string> Set { get; } = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
        {
            Hits++;
            return Task.FromResult<T?>(typed);
        }

        Misses++;
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        Set.Add(key);
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Removed.Add(key);
        _store.TryRemove(key, out _);
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
        await SetAsync(key, value, ttl, ct);
        return value;
    }
}
