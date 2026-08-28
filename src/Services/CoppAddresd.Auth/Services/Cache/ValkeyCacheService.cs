using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CoppAddresd.Auth.Services.Cache;

/// <summary>
/// Implementación de <see cref="ICacheService"/> sobre Valkey/Redis (en
/// producción, ElastiCache for Valkey). Fail-open por operación: cualquier
/// fallo de conexión/timeout/serialización se registra como Warning y la
/// operación se comporta como miss — el flujo de autorización resuelve contra
/// PostgreSQL y la petición nunca se rompe por el caché.
/// </summary>
public sealed class ValkeyCacheService(
    IConnectionMultiplexer multiplexer,
    string keyPrefix,
    ILogger<ValkeyCacheService> logger
) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    private readonly IDatabase _database = multiplexer.GetDatabase();

    private string FullKey(string key) => $"{keyPrefix}:{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(FullKey(key)).WaitAsync(ct);
            if (value.IsNull)
            {
                logger.LogDebug("Cache MISS {Key}", FullKey(key));
                return null;
            }

            logger.LogDebug("Cache HIT {Key}", FullKey(key));
            return JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Fallo de caché en GET {Key}: degradando a la fuente de datos",
                FullKey(key)
            );
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await _database.StringSetAsync(FullKey(key), payload, ttl).WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Fallo de caché en SET {Key}: el dato queda sin cachear",
                FullKey(key)
            );
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _database.KeyDeleteAsync(FullKey(key)).WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Fallo de caché en REMOVE {Key}: la invalidación quedará a cargo del TTL",
                FullKey(key)
            );
        }
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
