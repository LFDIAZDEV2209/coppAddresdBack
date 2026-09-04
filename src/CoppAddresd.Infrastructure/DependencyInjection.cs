using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Cache;
using CoppAddresd.Infrastructure.Metrics;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CoppAddresd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada."
            );

        services.AddScoped<AuditTriggerInterceptor>();

        services.AddDbContext<AppDbContext>(
            (serviceProvider, options) =>
                options
                    .UseNpgsql(
                        connectionString,
                        npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                    )
                    .AddInterceptors(serviceProvider.GetRequiredService<AuditTriggerInterceptor>())
        );

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditActorContext, HttpAuditActorContext>();

        services.AddScoped<IMediaItemRepository, MediaItemRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IAgentCatalogRepository, AgentCatalogRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IWellnessRepository, WellnessRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IHealthTestRepository, HealthTestRepository>();

        // Motor de scoring (Tests de Salud): estrategias registradas como
        // keyed services + registry. Agregar una estrategia nueva = registrar
        // la clase aquí (SPEC A9).
        services.AddKeyedSingleton<IScoreStrategy, SumScoreStrategy>(HealthTestScoringStrategy.sum);
        services.AddKeyedSingleton<IScoreStrategy, PercentageScoreStrategy>(
            HealthTestScoringStrategy.percentage
        );
        services.AddKeyedSingleton<IScoreStrategy, SubscaleScoreStrategy>(
            HealthTestScoringStrategy.subscale
        );
        services.AddKeyedSingleton<IScoreStrategy, InventoryScoreStrategy>(
            HealthTestScoringStrategy.inventory
        );
        services.AddKeyedSingleton<IScoreStrategy, WeightedScoreStrategy>(
            HealthTestScoringStrategy.weighted
        );
        services.AddSingleton<ScoreStrategyRegistry>();
        services.AddSingleton<ScoreRangeEngine>();
        services.AddSingleton<IndicatorEngine>();
        services.AddSingleton<AlertEngine>();

        // Notificaciones gamificadas (SPEC §20, "Paso 7b"): servicio best-effort
        // de la capa de aplicación + repositorio del log `app.notifications`.
        // El servicio reutiliza IFcmClient/IDeviceTokenRepository (registrados
        // arriba) y es consumido por ProgramRepository dentro de los flujos de
        // otorgamiento (hitos, día perfecto, subida de nivel) sin romper la
        // transacción de XP (AC-42).
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<IGamifiedNotificationService, GamifiedNotificationService>();

        // Catálogo de reglas XP (SPEC §14, B5-R): agregado separado de la
        // inscripción; los fakes de IProgramRepository de los tests no se
        // acoplan al catálogo.
        services.AddScoped<IXpRuleCatalogRepository, XpRuleCatalogRepository>();

        // Calculadores del motor de puntajes (SPEC §13, T-37/T-41): funciones
        // puras consumidas por ProgramRepository; registrados con su ILogger
        // real para que el log estructurado Program.ScoreComputed se emita
        // (sin DI caen al NullLogger del constructor opcional).
        services.AddScoped<IHealthScoreCalculator, HealthScoreCalculator>();
        services.AddScoped<ITransformationScoreCalculator, TransformationScoreCalculator>();

        // Detección de debilidades (SPEC §21, "Paso 7c"): servicio best-effort
        // de la capa de aplicación que orquesta el paquete semanal, el motor de
        // reglas (función pura) y la persistencia con dedupe (AC-43). Se
        // dispara SOLO en POST /scores/calculate (AC-45) vía
        // CalculateScoresCommandHandler.
        services.AddScoped<IWeaknessDetectionService, WeaknessDetectionService>();

        // Contexto clínico y reglas de seguridad para la generación de planes
        // con IA (servicios de aplicación + repositorios de lectura).
        services.AddScoped<IClinicalMeasurementRepository, ClinicalMeasurementRepository>();
        services.AddScoped<ISafetyRuleRepository, SafetyRuleRepository>();
        services.AddScoped<IClinicalContextService, ClinicalContextService>();
        services.AddScoped<ISafetyRulesService, SafetyRulesService>();

        services.AddMemoryCache();
        services.Configure<PostalCodeLookupOptions>(
            configuration.GetSection(PostalCodeLookupOptions.SectionName)
        );
        services.AddHttpClient(
            "Zippopotam",
            (serviceProvider, client) =>
            {
                var lookupOptions = serviceProvider
                    .GetRequiredService<IOptions<PostalCodeLookupOptions>>()
                    .Value;
                client.BaseAddress = new Uri(lookupOptions.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(lookupOptions.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CoppAddresd/1.0");
            }
        );
        services.AddScoped<IPostalCodeLookupService, ZippopotamPostalCodeLookup>();

        // Analítica — Pre-agregación CQRS de inventario (Dashboard #6): cola en memoria
        // + HostedService que drena eventos y hace upsert atómico sobre erp.inventory_daily_metrics.
        services.AddSingleton<IInventoryMetricsQueue, InventoryMetricsQueue>();
        services.AddHostedService<InventoryMetricsProcessorHostedService>();

        AddObjectStorage(services, configuration);

        AddDistributedCache(services, configuration);

        return services;
    }

    /// <summary>
    /// Caché distribuida compartida (Valkey) con degradación controlada. El
    /// proveedor se elige por configuración (<c>Cache:Provider</c>): Valkey
    /// (default: local en dev, ElastiCache for Valkey en prod vía
    /// <c>ConnectionStrings:Valkey</c>), Memory (tests/proceso único) o None
    /// (rollback: sin caché, todo a PostgreSQL). Fail-open por operación: un
    /// Valkey caído nunca rompe una request. Ver docs/modules/cache/README.md.
    /// </summary>
    private static void AddDistributedCache(
        IServiceCollection services,
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
                // Singleton: el multiplexer es thread-safe y multiplexa todas
                // las operaciones sobre una conexión (pooling nativo).
                services.AddSingleton<IConnectionMultiplexer>(_ =>
                {
                    var raw = connectionString ?? "localhost:6379";
                    var options = ConfigurationOptions.Parse(raw);
                    // Contrato fail-open: el arranque NUNCA se bloquea por
                    // caché ausente; las operaciones degradan por operación.
                    options.AbortOnConnectFail = false;
                    // Timeouts acotados salvo override explícito en la cadena.
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
                // Degraded (no Unhealthy): Valkey caído → /health responde 200
                // con el componente degradado; el servicio sigue operativo.
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
    }

    /// <summary>
    /// Registra la implementación de <see cref="IObjectStorageService"/> según
    /// <c>Storage:Provider</c> (por defecto, <c>Local</c>).
    /// </summary>
    private static void AddObjectStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? "Local";

        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<LocalStorageOptions>(
                configuration.GetSection(LocalStorageOptions.SectionName)
            );

            // Singleton: LocalObjectStorageService es stateless-safe (raíz inmutable,
            // operaciones de archivo por llamada, sin estado compartido).
            services.AddSingleton<IObjectStorageService, LocalObjectStorageService>();
            return;
        }

        if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<S3StorageOptions>(
                configuration.GetSection(S3StorageOptions.SectionName)
            );

            // Singleton: el AmazonS3Client es thread-safe y está diseñado para
            // reutilizarse. Las credenciales se resuelven por la cadena por defecto
            // del SDK (IAM role en producción); jamás Access Keys en configuración.
            services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();
            return;
        }

        throw new InvalidOperationException(
            $"Proveedor de almacenamiento desconocido: '{provider}'. "
                + "Valores soportados: 'Local', 'S3'."
        );
    }
}
