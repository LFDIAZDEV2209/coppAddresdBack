using CoppAddresd.Auth.Services.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CoppAddresd.Auth.Extensions;

/// <summary>
/// Registro DI del módulo de caché del Auth Service. Patrón idéntico al del
/// backend ERP (AddDistributedCache) pero autónomo: el Auth no referencia
/// proyectos del repo. Proveedor por <c>Cache:Provider</c> (Valkey|Memory|None),
/// claves namespaced con <c>auth:</c> y connection string
/// <c>ConnectionStrings:Valkey</c> (en prod, ElastiCache for Valkey).
/// </summary>
public static class AuthCacheExtensions
{
    public static IServiceCollection AddAuthCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        var provider = (configuration["Cache:Provider"] ?? CacheOptions.DefaultProvider).Trim();
        var keyPrefix = configuration["Cache:KeyPrefix"] ?? CacheOptions.DefaultKeyPrefix;
        var connectionString = configuration.GetConnectionString("Valkey");

        switch (provider.ToLowerInvariant())
        {
            case "valkey":
                // Singleton thread-safe con multiplexado: una conexión para
                // todas las operaciones de la instancia.
                services.AddSingleton<IConnectionMultiplexer>(_ =>
                {
                    var raw = connectionString ?? "127.0.0.1:6379";
                    var options = ConfigurationOptions.Parse(raw);
                    // Contrato fail-open: el arranque nunca se bloquea por
                    // caché ausente; las operaciones degradan por operación.
                    options.AbortOnConnectFail = false;
                    if (!raw.Contains("syncTimeout", StringComparison.OrdinalIgnoreCase))
                    {
                        options.SyncTimeout = 2000;
                    }
                    if (!raw.Contains("asyncTimeout", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AsyncTimeout = 2000;
                    }
                    if (!raw.Contains("connectTimeout", StringComparison.OrdinalIgnoreCase))
                    {
                        options.ConnectTimeout = 5000;
                    }

                    return ConnectionMultiplexer.Connect(options);
                });
                services.AddSingleton<ICacheService>(sp => new ValkeyCacheService(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    keyPrefix,
                    sp.GetRequiredService<ILogger<ValkeyCacheService>>()
                ));
                // Degraded (no Unhealthy): Valkey caído → /health 200 con el
                // componente degradado; el Auth Service sigue operativo.
                services
                    .AddHealthChecks()
                    .AddCheck<ValkeyHealthCheck>(
                        "valkey",
                        failureStatus: HealthStatus.Degraded,
                        tags: ["cache"]
                    );
                break;

            case "memory":
                services.AddMemoryCache();
                services.AddSingleton<ICacheService>(sp => new MemoryCacheService(
                    sp.GetRequiredService<IMemoryCache>(),
                    keyPrefix,
                    sp.GetRequiredService<ILogger<MemoryCacheService>>()
                ));
                break;

            case "none":
                services.AddSingleton<ICacheService, NoCacheService>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Cache:Provider desconocido: '{provider}'. Valores soportados: Valkey, Memory, None."
                );
        }

        return services;
    }
}
