using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Infrastructure.Cache;
using CoppAddresd.Telemedicine.Infrastructure.Configuration;
using CoppAddresd.Telemedicine.Infrastructure.Extensions;
using CoppAddresd.Telemedicine.Infrastructure.Metrics;
using CoppAddresd.Telemedicine.Infrastructure.Notifications;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.Infrastructure.Security;
using CoppAddresd.Telemedicine.Infrastructure.Services;
using CoppAddresd.Telemedicine.Infrastructure.Sessions;
using CoppAddresd.Telemedicine.Infrastructure.VideoProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CoppAddresd.Telemedicine.Infrastructure;

/// <summary>
/// Registro de dependencias de infraestructura: DbContext (schema <c>tele</c>),
/// servicios de datos y el proveedor de video seleccionado por configuración
/// (<c>Telemedicine:Provider</c>). Cambiar de proveedor = cambiar configuración.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTelemedicineInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada para Telemedicina."
            );

        services.AddHttpContextAccessor();
        services.AddScoped<HttpAuditActorContext>();

        services.AddDbContext<TelemedicineDbContext>(
            (serviceProvider, options) =>
                options
                    .UseNpgsql(
                        connectionString,
                        npgsql =>
                            npgsql
                                .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                                // Historial de migraciones aislado en el schema tele:
                                // la instancia compartida tiene su historial en public.
                                .MigrationsHistoryTable("__ef_migrations_history", "tele")
                    )
                    .UseSnakeCaseNamingConvention()
                    // Auditoría (Fase 12): propaga actor JWT + correlación a los GUC
                    // audit.* al iniciar cada transacción (trigger del encuentro clínico).
                    .AddInterceptors(serviceProvider.GetRequiredService<AuditTriggerInterceptor>())
        );
        services.AddScoped<AuditTriggerInterceptor>();

        services.AddMemoryCache();

        services.AddScoped<ITelemedicineSettingsProvider, TelemedicineSettingsProvider>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ITelemedicineUnitOfWork, TelemedicineUnitOfWork>();
        services.AddScoped<INotificationDispatchRepository, NotificationDispatchRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IPreVisitIntakeRepository, PreVisitIntakeRepository>();
        services.AddScoped<IEncounterAddendumRepository, EncounterAddendumRepository>();

        // Métricas analíticas pre-agregadas en segundo plano (Fase 1 Pre-agregación CQRS)
        services.AddSingleton<ITelemedicineMetricsQueue, TelemedicineMetricsQueue>();
        services.AddHostedService<TelemedicineMetricsProcessorHostedService>();

        // Barrido periódico de sesiones estancadas: cierra citas InProgress cuyo
        // fin programado ya pasó (más la gracia de la ventana de sala efectiva).
        services.AddScoped<StaleSessionSweeper>();
        services.AddHostedService<StaleSessionSweepHostedService>();

        // F2: barrido de recordatorios de citas confirmadas (push/SMS vía backend)
        // con deduplicación en tele.notification_dispatch.
        services.AddScoped<AppointmentReminderSweeper>();
        services.AddHostedService<AppointmentReminderSweepHostedService>();

        // Backfill/reparación de las métricas pre-agregadas (operación admin).
        services.AddScoped<IMetricsBackfillService, MetricsBackfillService>();

        services.Configure<Application.Configuration.TelemedicineOptions>(
            configuration.GetSection(Application.Configuration.TelemedicineOptions.SectionName)
        );

        AddBackendReferenceDataClient(services, configuration);

        AddBackendNotifierClient(services, configuration);

        AddAuthScopedAuthorizationClient(services, configuration);

        AddVideoProvider(services, configuration);

        AddDistributedCache(services, configuration);

        return services;
    }

    /// <summary>
    /// Caché distribuida compartida (Valkey) con degradación controlada: el
    /// proveedor se elige por <c>Cache:Provider</c> (Valkey|Memory|None), con
    /// claves namespaced <c>tele:...</c> y connection string
    /// <c>ConnectionStrings:Valkey</c> (en prod, ElastiCache for Valkey).
    /// Fail-open por operación: un Valkey caído nunca rompe una petición.
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
                // Singleton thread-safe con multiplexado (una conexión por instancia).
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
                // componente degradado; el micro sigue operativo.
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
    /// Cliente de introspección de permisos hacia el Auth Service
    /// (<c>AuthService:BaseUrl</c> + header <c>X-Internal-Key</c>): evalúa los
    /// permisos efectivos (claims ∪ scoped por clínica) de los roles asignados
    /// con scope, con resiliencia estándar del proyecto.
    /// </summary>
    private static void AddAuthScopedAuthorizationClient(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AuthServiceSettings>(
            configuration.GetSection(AuthServiceSettings.SectionName)
        );

        services
            .AddHttpClient<
                ITelemedicineScopedAuthorizationClient,
                TelemedicineScopedAuthorizationClient
            >(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();
    }

    /// <summary>
    /// Cliente de datos de referencia hacia el backend del ERP
    /// (<c>Backend:BaseUrl</c> + header <c>X-Internal-Key</c>), con resiliencia
    /// estándar del proyecto (reintentos + circuit breaker).
    /// </summary>
    private static void AddBackendReferenceDataClient(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<BackendServiceSettings>(
            configuration.GetSection(BackendServiceSettings.SectionName)
        );

        services
            .AddHttpClient<IAppointmentReferenceDataService, AppointmentReferenceDataService>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<BackendServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();
    }

    /// <summary>
    /// Cliente de entrega de notificaciones hacia el backend del ERP (F2,
    /// <c>Backend:BaseUrl</c> + header <c>X-Internal-Key</c>, misma configuración
    /// que los datos de referencia), con resiliencia estándar del proyecto.
    /// </summary>
    private static void AddBackendNotifierClient(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<BackendServiceSettings>(
            configuration.GetSection(BackendServiceSettings.SectionName)
        );

        services
            .AddHttpClient<ITelemedicineNotifier, TelemedicineNotifier>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<BackendServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();
    }

    /// <summary>
    /// Registra la implementación de <see cref="IVideoProvider"/> según
    /// <c>Telemedicine:Provider</c> (por defecto, <c>twilio</c>). Añadir un
    /// proveedor futuro = nueva clase + un <c>else if</c> aquí.
    /// </summary>
    private static void AddVideoProvider(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));

        var provider = configuration["Telemedicine:Provider"] ?? "twilio";

        if (provider.Equals("twilio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IVideoProvider>(sp =>
            {
                var twilioOptions = sp.GetRequiredService<IOptions<TwilioOptions>>().Value;
                if (!twilioOptions.IsConfigured)
                {
                    throw new InvalidOperationException(
                        "Twilio no configurado: define Twilio:AccountSid/ApiKeySid/ApiKeySecret."
                    );
                }
                return ActivatorUtilities.CreateInstance<TwilioVideoProvider>(sp);
            });
            return;
        }

        throw new InvalidOperationException(
            $"Proveedor de video desconocido: '{provider}'. Valores soportados: 'twilio'."
        );
    }
}
