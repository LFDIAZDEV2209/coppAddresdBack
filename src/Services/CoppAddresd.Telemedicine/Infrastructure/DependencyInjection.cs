using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Infrastructure.Configuration;
using CoppAddresd.Telemedicine.Infrastructure.Extensions;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.Infrastructure.Security;
using CoppAddresd.Telemedicine.Infrastructure.Services;
using CoppAddresd.Telemedicine.Infrastructure.VideoProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada para Telemedicina.");

        services.AddHttpContextAccessor();
        services.AddScoped<HttpAuditActorContext>();

        services.AddDbContext<TelemedicineDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql
                        .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                        // Historial de migraciones aislado en el schema tele:
                        // la instancia compartida tiene su historial en public.
                        .MigrationsHistoryTable("__ef_migrations_history", "tele"))
                .UseSnakeCaseNamingConvention()
                // Auditoría (Fase 12): propaga actor JWT + correlación a los GUC
                // audit.* al iniciar cada transacción (trigger del encuentro clínico).
                .AddInterceptors(serviceProvider.GetRequiredService<AuditTriggerInterceptor>()));
        services.AddScoped<AuditTriggerInterceptor>();

        services.AddMemoryCache();

        services.AddScoped<ITelemedicineSettingsProvider, TelemedicineSettingsProvider>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ITelemedicineUnitOfWork, TelemedicineUnitOfWork>();

        services.Configure<Application.Configuration.TelemedicineOptions>(
            configuration.GetSection(Application.Configuration.TelemedicineOptions.SectionName));

        AddBackendReferenceDataClient(services, configuration);

        AddAuthScopedAuthorizationClient(services, configuration);

        AddVideoProvider(services, configuration);

        return services;
    }

    /// <summary>
    /// Cliente de introspección de permisos hacia el Auth Service
    /// (<c>AuthService:BaseUrl</c> + header <c>X-Internal-Key</c>): evalúa los
    /// permisos efectivos (claims ∪ scoped por clínica) de los roles asignados
    /// con scope, con resiliencia estándar del proyecto.
    /// </summary>
    private static void AddAuthScopedAuthorizationClient(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthServiceSettings>(
            configuration.GetSection(AuthServiceSettings.SectionName));

        services.AddHttpClient<ITelemedicineScopedAuthorizationClient, TelemedicineScopedAuthorizationClient>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
                })
            .AddResiliencePolicy();
    }

    /// <summary>
    /// Cliente de datos de referencia hacia el backend del ERP
    /// (<c>Backend:BaseUrl</c> + header <c>X-Internal-Key</c>), con resiliencia
    /// estándar del proyecto (reintentos + circuit breaker).
    /// </summary>
    private static void AddBackendReferenceDataClient(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BackendServiceSettings>(
            configuration.GetSection(BackendServiceSettings.SectionName));

        services.AddHttpClient<IAppointmentReferenceDataService, AppointmentReferenceDataService>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<BackendServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
                })
            .AddResiliencePolicy();
    }

    /// <summary>
    /// Registra la implementación de <see cref="IVideoProvider"/> según
    /// <c>Telemedicine:Provider</c> (por defecto, <c>twilio</c>). Añadir un
    /// proveedor futuro = nueva clase + un <c>else if</c> aquí.
    /// </summary>
    private static void AddVideoProvider(
        IServiceCollection services,
        IConfiguration configuration)
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
                        "Twilio no configurado: define Twilio:AccountSid/ApiKeySid/ApiKeySecret.");
                }
                return ActivatorUtilities.CreateInstance<TwilioVideoProvider>(sp);
            });
            return;
        }

        throw new InvalidOperationException(
            $"Proveedor de video desconocido: '{provider}'. Valores soportados: 'twilio'.");
    }
}
