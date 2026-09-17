using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Implementación de <see cref="ICacheService"/> sobre Valkey/Redis (en
/// producción, ElastiCache for Valkey). Fail-open por operación: cualquier
/// fallo se registra como Warning y la operación se comporta como miss.
///
/// Circuit breaker: con Valkey caído cada operación esperaría el timeout del
/// multiplexer; tras 2 fallos consecutivos el breaker se abre 30 s y las
/// siguientes operaciones fallan instantáneo (miss) hasta el reintento
/// half-open. Se re-cierra solo cuando Valkey vuelve (éxito resetea).
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

    internal static int FailureThreshold = 2;
    internal static TimeSpan OpenWindow = TimeSpan.FromSeconds(30);

    private int _consecutiveFailures;

    /// <summary>long no puede ser volatile: acceso con Interlocked/Volatile.Read.</summary>
    private long _lastFailureTick = -1;

    private readonly IDatabase _database = multiplexer.GetDatabase();

    private string FullKey(string key) => $"{keyPrefix}:{key}";

    private bool IsCircuitOpen()
    {
        var last = Volatile.Read(ref _lastFailureTick);
        return last >= 0
            && Environment.TickCount64 - last < OpenWindow.TotalMilliseconds
            && Volatile.Read(ref _consecutiveFailures) >= FailureThreshold;
    }

    private void RegisterFailure(Exception ex, string operation, string key)
    {
        Volatile.Write(ref _lastFailureTick, Environment.TickCount64);
        Interlocked.Increment(ref _consecutiveFailures);
        logger.LogWarning(
            ex,
            "Fallo de caché en {Operation} {Key} (intento {Failures}): degradando a la fuente de datos",
            operation,
            FullKey(key),
            Volatile.Read(ref _consecutiveFailures)
        );
    }

    private void RegisterSuccess()
    {
        Volatile.Write(ref _lastFailureTick, -1);
        Volatile.Write(ref _consecutiveFailures, 0);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        if (IsCircuitOpen())
        {
            logger.LogDebug("Cache breaker ABIERTO: GET {Key} omitido", FullKey(key));
            return null;
        }

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
            RegisterFailure(ex, "GET", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        if (IsCircuitOpen())
        {
            logger.LogDebug("Cache breaker ABIERTO: SET {Key} omitido", FullKey(key));
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await _database.StringSetAsync(FullKey(key), payload, ttl).WaitAsync(ct);
            RegisterSuccess();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RegisterFailure(ex, "SET", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            logger.LogDebug("Cache breaker ABIERTO: REMOVE {Key} omitido", FullKey(key));
            return;
        }

        try
        {
            await _database.KeyDeleteAsync(FullKey(key)).WaitAsync(ct);
            RegisterSuccess();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RegisterFailure(ex, "REMOVE", key);
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

        // La factory puede devolver null (p. ej. recurso no encontrado): el
        // miss no se cachea y el contrato Task<T> propaga ese null tal cual.
        return value!;
    }
}
