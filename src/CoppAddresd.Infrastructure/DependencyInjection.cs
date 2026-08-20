using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada.");

        services.AddScoped<AuditTriggerInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditTriggerInterceptor>()));

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditActorContext, HttpAuditActorContext>();

        services.AddScoped<IMediaItemRepository, MediaItemRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IAgentCatalogRepository, AgentCatalogRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();

        services.AddMemoryCache();
        services.Configure<PostalCodeLookupOptions>(
            configuration.GetSection(PostalCodeLookupOptions.SectionName));
        services.AddHttpClient("Zippopotam", (serviceProvider, client) =>
        {
            var lookupOptions = serviceProvider
                .GetRequiredService<IOptions<PostalCodeLookupOptions>>().Value;
            client.BaseAddress = new Uri(lookupOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(lookupOptions.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CoppAddresd/1.0");
        });
        services.AddScoped<IPostalCodeLookupService, ZippopotamPostalCodeLookup>();

        AddObjectStorage(services, configuration);

        return services;
    }

    /// <summary>
    /// Registra la implementación de <see cref="IObjectStorageService"/> según
    /// <c>Storage:Provider</c> (por defecto, <c>Local</c>).
    /// </summary>
    private static void AddObjectStorage(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? "Local";

        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<LocalStorageOptions>(
                configuration.GetSection(LocalStorageOptions.SectionName));

            // Singleton: LocalObjectStorageService es stateless-safe (raíz inmutable,
            // operaciones de archivo por llamada, sin estado compartido).
            services.AddSingleton<IObjectStorageService, LocalObjectStorageService>();
            return;
        }

        if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            // Punto de extensión para el futuro S3ObjectStorageService (AWSSDK.S3).
            // Se implementará cuando el equipo entregue las credenciales de AWS.
            throw new InvalidOperationException(
                "El proveedor de almacenamiento 'S3' aún no está implementado. " +
                "Agregue S3ObjectStorageService y el paquete AWSSDK.S3 cuando la " +
                "configuración de AWS esté disponible.");
        }

        throw new InvalidOperationException(
            $"Proveedor de almacenamiento desconocido: '{provider}'. " +
            "Valores soportados: 'Local'.");
    }
}
