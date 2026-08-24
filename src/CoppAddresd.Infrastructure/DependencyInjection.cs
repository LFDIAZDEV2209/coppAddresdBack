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
            services.Configure<S3StorageOptions>(
                configuration.GetSection(S3StorageOptions.SectionName));

            // Singleton: el AmazonS3Client es thread-safe y está diseñado para
            // reutilizarse. Las credenciales se resuelven por la cadena por defecto
            // del SDK (IAM role en producción); jamás Access Keys en configuración.
            services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();
            return;
        }

        throw new InvalidOperationException(
            $"Proveedor de almacenamiento desconocido: '{provider}'. " +
            "Valores soportados: 'Local', 'S3'.");
    }
}
