using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.HealthTests.Notifications;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using CoppAddresd.Application.Features.Redes;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Cache;
using CoppAddresd.Infrastructure.Metrics;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using CoppAddresd.Infrastructure.Services.Email;
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
        services.AddScoped<IPatientDashboardRepository, PatientDashboardRepository>();
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
        services.AddScoped<IHealthTestNotificationRepository, HealthTestNotificationRepository>();
        services.AddScoped<IRedesRepository, RedesRepository>();

        // Validador de CUV (Acceso a Redes): determinístico mock en Application;
        // la integración real con el validador de plataforma lo implementa en
        // Infrastructure detrás de la misma interfaz.
        services.AddScoped<ICuvValidator, DeterministicCuvValidator>();
        services.AddSingleton<IHealthTestTemplateRenderer, HealthTestTemplateRenderer>();

        // Controles del programa (Program Controls): repositorio enfocado de
        // program_controls (precedente: DeviceTokenRepository/LeagueRepository
        // — no crece ProgramRepository).
        services.AddScoped<IProgramControlRepository, ProgramControlRepository>();

        // Liga del paciente (LEAGUE v1): repositorio enfocado de solo lectura
        // + update mínimo de preferencias (no crece ProgramRepository).
        services.AddScoped<ILeagueRepository, LeagueRepository>();

        // Historial de métricas clínicas (metrics-history): repositorio
        // enfocado de solo lectura (precedente ScoresHistoryRepository).
        services.AddScoped<IMetricsHistoryRepository, MetricsHistoryRepository>();

        // Historial de puntajes del paciente (scores-history): repositorio
        // enfocado de solo lectura (no crece ProgramRepository, precedente:
        // LeagueRepository). SOLO filas persistidas — nunca dispara recálculo.
        services.AddScoped<IScoresHistoryRepository, ScoresHistoryRepository>();

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

        // Pre-agregación de métricas del Programa ANTARES (Dashboard O(1) en Background)
        services.AddSingleton<IProgramMetricsQueue, ProgramMetricsQueue>();
        services.AddHostedService<ProgramMetricsProcessorHostedService>();

        // Pre-agregación de métricas de Pacientes y Directorio Clínico (Fase 1 Pre-agregación CQRS)
        services.AddSingleton<IPatientMetricsQueue, PatientMetricsQueue>();
        services.AddHostedService<PatientMetricsProcessorHostedService>();

        // Pre-agregación de métricas de Tests de Salud y Baterías Clínicas (Fase 1 Pre-agregación CQRS)
        services.AddSingleton<IHealthTestMetricsQueue, HealthTestMetricsQueue>();
        services.AddHostedService<HealthTestMetricsProcessorHostedService>();

        // Pre-agregación de métricas de Inventario y Farmacia (Dashboard #6, Fase 1 CQRS)
        services.AddSingleton<IInventoryMetricsQueue, InventoryMetricsQueue>();
        services.AddHostedService<InventoryMetricsProcessorHostedService>();

        // Pre-agregación de métricas Biométricas Clínicas (CQRS Channel Pattern)
        services.AddSingleton<IBiometriaMetricsQueue, BiometriaMetricsQueue>();
        services.AddHostedService<BiometriaMetricsProcessorHostedService>();

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

        // Resolvedor de contenido del programa (SPEC §4.2/§4.3/§6.10 — T-74):
        // servicio de solo lectura que determina el plan de alimentación activo y
        // la rutina de ejercicio activa para un paciente en una fecha local.
        // Consume IWellnessRepository (ya registrado arriba).
        services.AddScoped<IProgramContentResolver, ProgramContentResolver>();

        // Motor de reglas de adaptación (SPEC §6.8, T-23): funciones puras y
        // deterministas evaluadas por ProgramRepository tras cada completación
        // (dentro de la misma transacción). Sin estado: Scoped por consistencia.
        services.AddScoped<IProgramAdaptationEngine, ProgramAdaptationEngine>();

        // Job de reconciliación de rachas (B12, T-28): orquesta la pasada de
        // recálculo de streak_states. Lo consume el hosted service nocturno
        // (ReconcileStreakHostedService en la API) y el disparo manual
        // (POST /program/maintenance/reconcile-streaks).
        services.AddScoped<ReconcileStreakJob>();

        // Contexto clínico y reglas de seguridad para la generación de planes
        // con IA (servicios de aplicación + repositorios de lectura).
        services.AddScoped<IClinicalMeasurementRepository, ClinicalMeasurementRepository>();
        services.AddScoped<ISafetyRuleRepository, SafetyRuleRepository>();
        services.AddScoped<IClinicalContextService, ClinicalContextService>();
        services.AddScoped<ISafetyRulesService, SafetyRulesService>();

        // Compresión de exámenes de laboratorio
        services.AddSingleton<IFileCompressionService, FileCompressionService>();

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

        AddObjectStorage(services, configuration);
        AddEmailServices(services, configuration);
        AddSmsSender(services, configuration);

        AddDistributedCache(services, configuration);

        return services;
    }

    /// <summary>
    /// Registra la implementación de <see cref="IEmailService"/> según <c>Email:Provider</c>
    /// (por defecto, <c>Log</c> para desarrollo sin credenciales).
    /// </summary>
    private static void AddEmailServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration["Email:Provider"] ?? "Log";

        if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            // Default: Log (seguro para desarrollo sin credenciales)
            services.AddScoped<IEmailService, LogEmailService>();
        }
    }

    /// <summary>
    /// Registra la implementación de <see cref="ISmsSender"/> según <c>Sms:Provider</c>
    /// (SPEC A13). Hoy solo existe la implementación <c>Noop</c> (registra el envío
    /// simulado, sin proveedor externo); un proveedor real (Twilio Messages, SNS...)
    /// se enchufa detrás de la misma interfaz cuando se configuren credenciales.
    /// </summary>
    private static void AddSmsSender(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));

        services.AddScoped<ISmsSender, NoOpSmsSender>();
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
                    // localhost resuelve primero a ::1 (IPv6) y falla cuando Valkey solo escucha IPv4.
                    var raw = connectionString ?? "127.0.0.1:6379";
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
