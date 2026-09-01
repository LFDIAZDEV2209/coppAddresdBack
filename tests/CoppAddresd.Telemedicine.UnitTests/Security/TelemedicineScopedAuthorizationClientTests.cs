using System.Net;
using CoppAddresd.Telemedicine.Infrastructure.Configuration;
using CoppAddresd.Telemedicine.Infrastructure.Security;
using CoppAddresd.Telemedicine.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.UnitTests.Security;

/// <summary>
/// Introspección scoped con caché distribuida (SPEC infra/cache): la segunda
/// llamada con el mismo (usuario, stamp, permiso, scopes) NO repite la
/// introspección HTTP (hit), y un stamp distinto (rotación) produce miss.
/// El fake de caché cuenta las llamadas de fábrica HTTP.
/// </summary>
public sealed class TelemedicineScopedAuthorizationClientTests
{
    private static (TelemedicineScopedAuthorizationClient Client, CountingHandler Handler) Create(
        FakeCacheService cache
    )
    {
        var handler = new CountingHandler("""{"allowed":true}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5123") };
        var client = new TelemedicineScopedAuthorizationClient(
            httpClient,
            Options.Create(new AuthServiceSettings { BaseUrl = "http://localhost:5123" }),
            cache,
            NullLogger<TelemedicineScopedAuthorizationClient>.Instance
        );
        return (client, handler);
    }

    [Fact]
    public async Task AuthorizeAsync_MismoStamp_SegundaLlamadaEsHitSinHttp()
    {
        var cache = new FakeCacheService();
        var (client, handler) = Create(cache);
        var userId = Guid.NewGuid();
        var chain = new List<ScopeEntry> { new("Clinic", Guid.NewGuid()) };

        var first = await client.AuthorizeAsync(userId, "stamp-1", "Appointments.View", chain);
        var second = await client.AuthorizeAsync(userId, "stamp-1", "Appointments.View", chain);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, cache.Hits);
    }

    [Fact]
    public async Task AuthorizeAsync_StampRotado_EsMiss()
    {
        var cache = new FakeCacheService();
        var (client, handler) = Create(cache);
        var userId = Guid.NewGuid();
        var chain = new List<ScopeEntry> { ScopeEntry.Global };

        await client.AuthorizeAsync(userId, "stamp-1", "Appointments.View", chain);
        await client.AuthorizeAsync(userId, "stamp-2", "Appointments.View", chain);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task AuthorizeAsync_LaClaveLlevaVersion()
    {
        var cache = new FakeCacheService();
        var (client, _) = Create(cache);

        await client.AuthorizeAsync(
            Guid.NewGuid(),
            "stamp-1",
            "Appointments.View",
            [ScopeEntry.Global]
        );

        var key = Assert.Single(cache.Set);
        Assert.EndsWith(":v1", key);
        Assert.StartsWith("scope:", key);
    }
}
