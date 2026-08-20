using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Verifica la selección automática del proveedor de storage por configuración
/// (<c>Storage:Provider</c>): <c>Local</c> → <c>LocalObjectStorageService</c>,
/// <c>S3</c> → <c>S3ObjectStorageService</c>. Inspecciona los descriptores de
/// DI sin resolver ni instanciar (evita la cadena de credenciales de AWS).
/// </summary>
public class StorageProviderSelectionTests
{
    private static IConfiguration BuildConfig(string provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=coppaddresd;Username=app_user;Password=x",
                ["Storage:Provider"] = provider,
            })
            .Build();

    private static IConfiguration BuildConfigWithoutProvider() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=coppaddresd;Username=app_user;Password=x",
            })
            .Build();

    [Fact]
    public void Provider_Local_registra_LocalObjectStorageService()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfig("Local"));

        var descriptor = services.Single(d => d.ServiceType == typeof(IObjectStorageService));
        Assert.Equal(typeof(LocalObjectStorageService), descriptor.ImplementationType);
    }

    [Fact]
    public void Provider_S3_registra_S3ObjectStorageService()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfig("S3"));

        var descriptor = services.Single(d => d.ServiceType == typeof(IObjectStorageService));
        Assert.Equal(typeof(S3ObjectStorageService), descriptor.ImplementationType);
    }

    [Fact]
    public void Provider_desconocido_lanza_InvalidOperation()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(BuildConfig("Azure")));
        Assert.Contains("desconocido", ex.Message);
    }

    [Fact]
    public void Provider_ausente_defaults_a_Local()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfigWithoutProvider());

        var descriptor = services.Single(d => d.ServiceType == typeof(IObjectStorageService));
        Assert.Equal(typeof(LocalObjectStorageService), descriptor.ImplementationType);
    }
}
