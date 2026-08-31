using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using CoppAddresd.Community.Security;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Community.Storage;

/// <summary>
/// Registro del almacenamiento de objetos para el servicio de comunidad.
/// Espeja el comportamiento del helper compartido (<c>Storage:Provider</c>,
/// por defecto <c>Local</c>) sin arrastrar el resto de la infraestructura del
/// API principal: Community es un servicio autónomo.
/// </summary>
public static class StorageRegistrar
{
    public static IServiceCollection AddCommunityStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? "Local";

        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<LocalStorageOptions>(
                configuration.GetSection(LocalStorageOptions.SectionName));
            services.AddSingleton<IObjectStorageService, LocalObjectStorageService>();
        }
        else if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<S3StorageOptions>(
                configuration.GetSection(S3StorageOptions.SectionName));
            services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Proveedor de almacenamiento desconocido: '{provider}'. " +
                "Valores soportados: 'Local', 'S3'.");
        }

        var signatureKey = configuration["Storage:SignatureKey"]
            ?? throw new InvalidOperationException(
                "Storage:SignatureKey no configurado. Define el secreto en appsettings " +
                "o variables de entorno (es la clave HMAC de las URLs firmadas locales).");

        services.AddSingleton(new StorageSignatureService(signatureKey));

        return services;
    }
}
