using CoppAddresd.Infrastructure.Cache;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace CoppAddresd.UnitTests.Cache;

/// <summary>
/// Pruebas de integración contra el Valkey local del docker-compose
/// (puerto 6379). Se AUTO-SKIP si no hay Valkey disponible para que la
/// suite pase limpia en CI sin infraestructura. La conexión se puede
/// sobrescribir con la variable de entorno COP_VALKEY_TEST_CONNECTION
/// (formato StackExchange.Redis: host:puerto,password=...).
/// </summary>
public sealed class ValkeyCacheServiceLocalValkeyTests
{
    private const string DefaultLocalConnection = "127.0.0.1:6379,password=CoppAddresdValkey2026";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private static async Task<bool> TryCreateServiceAsync(ValkeyCacheServiceHolder holder)
    {
        var raw =
            Environment.GetEnvironmentVariable("COP_VALKEY_TEST_CONNECTION")
            ?? DefaultLocalConnection;
        var options = ConfigurationOptions.Parse(raw);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 1500;
        options.ConnectRetry = 0;

        try
        {
            var multiplexer = await ConnectionMultiplexer
                .ConnectAsync(options)
                .WaitAsync(ProbeTimeout);
            // Probe ESTRICTO: AbortOnConnectFail=false hace que ConnectAsync no
            // lance aunque no conecte a nada, así que se verifica con un PING
            // real. Sin Valkey vivo los tests se saltan (suite limpia en CI).
            await multiplexer.GetDatabase().PingAsync().WaitAsync(ProbeTimeout);
            holder.Service = new ValkeyCacheService(
                multiplexer,
                "erp",
                NullLogger<ValkeyCacheService>.Instance
            );
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Fact]
    public async Task Roundtrip_SetGetRemove_SobreValkeyReal()
    {
        var holder = new ValkeyCacheServiceHolder();
        if (!await TryCreateServiceAsync(holder))
        {
            return; // Sin Valkey disponible: la suite sigue limpia en CI.
        }

        var service = holder.Service!;
        var key = $"tests:roundtrip:{Guid.NewGuid():N}:v1";
        try
        {
            var original = new Dictionary<string, string>
            {
                ["code"] = "O+",
                ["name"] = "O positivo",
            };

            await service.SetAsync(key, original, TimeSpan.FromSeconds(30));
            var read = await service.GetAsync<Dictionary<string, string>>(key);

            Assert.NotNull(read);
            Assert.Equal("O+", read["code"]);

            await service.RemoveAsync(key);
            var afterRemove = await service.GetAsync<Dictionary<string, string>>(key);
            Assert.Null(afterRemove);
        }
        finally
        {
            await service.RemoveAsync(key);
        }
    }

    [Fact]
    public async Task Expiracion_ClaveConTtlCorto_SaleDelCache()
    {
        var holder = new ValkeyCacheServiceHolder();
        if (!await TryCreateServiceAsync(holder))
        {
            return;
        }

        var service = holder.Service!;
        var key = $"tests:ttl:{Guid.NewGuid():N}:v1";

        await service.SetAsync(
            key,
            new Dictionary<string, string> { ["v"] = "1" },
            TimeSpan.FromSeconds(1)
        );
        var inmediato = await service.GetAsync<Dictionary<string, string>>(key);
        await Task.Delay(1500);
        var expirado = await service.GetAsync<Dictionary<string, string>>(key);

        Assert.NotNull(inmediato);
        Assert.Null(expirado);
    }

    [Fact]
    public async Task GetOrCreate_SegundaLlamada_EsHit()
    {
        var holder = new ValkeyCacheServiceHolder();
        if (!await TryCreateServiceAsync(holder))
        {
            return;
        }

        var service = holder.Service!;
        var key = $"tests:getorcreate:{Guid.NewGuid():N}:v1";
        var llamadas = 0;

        var first = await service.GetOrCreateAsync(
            key,
            TimeSpan.FromSeconds(30),
            _ =>
            {
                llamadas++;
                return Task.FromResult<Dictionary<string, string>>(new() { ["v"] = "1" });
            }
        );
        var second = await service.GetOrCreateAsync(
            key,
            TimeSpan.FromSeconds(30),
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

    /// <summary>Contenedor mutable para pasar el servicio creado en el probe.</summary>
    private sealed class ValkeyCacheServiceHolder
    {
        public ValkeyCacheService? Service { get; set; }
    }
}
