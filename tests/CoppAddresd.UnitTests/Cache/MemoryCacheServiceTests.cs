using CoppAddresd.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Cache;

/// <summary>
/// Semántica hit/miss y prefijado de <see cref="MemoryCacheService"/> (la
/// variante <c>Cache:Provider=Memory</c>). Las claves deben quedar
/// namespaced por servicio: <c>&lt;prefijo&gt;:&lt;clave&gt;</c>.
/// </summary>
public sealed class MemoryCacheServiceTests
{
    private static (MemoryCacheService Service, IMemoryCache Cache) Create(string prefix = "erp")
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (
            new MemoryCacheService(cache, prefix, NullLogger<MemoryCacheService>.Instance),
            cache
        );
    }

    [Fact]
    public async Task Get_ClaveInexistente_DevuelveNull()
    {
        var (service, _) = Create();

        var result = await service.GetAsync<Dictionary<string, string>>("catalog:test:v1");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetThenGet_DevuelveElMismoValor()
    {
        var (service, _) = Create();
        var original = new Dictionary<string, string> { ["id"] = "123" };

        await service.SetAsync("catalog:test:v1", original, TimeSpan.FromMinutes(5));
        var result = await service.GetAsync<Dictionary<string, string>>("catalog:test:v1");

        Assert.NotNull(result);
        Assert.Equal("123", result["id"]);
    }

    [Fact]
    public async Task Set_EscribeConPrefijoDeServicio()
    {
        var (service, cache) = Create("erp");

        await service.SetAsync(
            "catalog:test:v1",
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(5)
        );

        Assert.True(cache.TryGetValue("erp:catalog:test:v1", out _));
    }

    [Fact]
    public async Task Remove_EliminaLaClave()
    {
        var (service, _) = Create();
        await service.SetAsync(
            "catalog:test:v1",
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(5)
        );

        await service.RemoveAsync("catalog:test:v1");
        var result = await service.GetAsync<Dictionary<string, string>>("catalog:test:v1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreate_EnMiss_EjecutaFactoryUnaVez()
    {
        var (service, _) = Create();
        var llamadas = 0;

        var first = await service.GetOrCreateAsync(
            "catalog:test:v1",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                llamadas++;
                return Task.FromResult<Dictionary<string, string>>(new() { ["v"] = "1" });
            }
        );
        var second = await service.GetOrCreateAsync(
            "catalog:test:v1",
            TimeSpan.FromMinutes(5),
            _ =>
            {
                llamadas++;
                return Task.FromResult<Dictionary<string, string>>(new() { ["v"] = "2" });
            }
        );

        Assert.Equal(1, llamadas);
        Assert.Equal("1", first["v"]);
        Assert.Equal("1", second["v"]);
    }

    [Fact]
    public async Task GetOrCreate_ConTtlExpirado_Reconstruye()
    {
        var (service, _) = Create();

        await service.SetAsync(
            "catalog:test:v1",
            new Dictionary<string, string> { ["v"] = "1" },
            TimeSpan.FromMilliseconds(50)
        );
        await Task.Delay(80);
        var result = await service.GetOrCreateAsync(
            "catalog:test:v1",
            TimeSpan.FromMinutes(5),
            _ => Task.FromResult<Dictionary<string, string>>(new() { ["v"] = "2" })
        );

        Assert.Equal("2", result["v"]);
    }
}
