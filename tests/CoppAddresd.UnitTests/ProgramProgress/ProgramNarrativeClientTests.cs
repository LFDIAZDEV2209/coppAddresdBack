using System.Net;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class ProgramNarrativeClientTests
{
    private static ProgramNarrativeRequest Payload() => new(JsonSerializer.Deserialize<JsonElement>("{\"facts\":{}}"),
        JsonSerializer.Deserialize<JsonElement>("{\"matchedRules\":[]}"), ["ADA 2026"]);

    [Fact]
    public async Task Cliente_EnviaContratoYClaveInterna_SinPermitirQueLaIaElijaContenido()
    {
        var transport = new Transport(async (request, ct) =>
        {
            Assert.Equal("/internal/program/assignment-narrative", request.RequestUri!.AbsolutePath);
            Assert.Equal("clave-simulada", Assert.Single(request.Headers.GetValues("X-Internal-Key")));
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("es", json.RootElement.GetProperty("language").GetString());
            Assert.True(json.RootElement.TryGetProperty("profile_snapshot", out _));
            Assert.True(json.RootElement.GetProperty("proposal").TryGetProperty("matchedRules", out _));
            Assert.Equal(new[] { "language", "profile_snapshot", "proposal", "source_refs" },
                json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            Assert.Equal("ADA 2026", Assert.Single(json.RootElement.GetProperty("source_refs").EnumerateArray()).GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """{"narratives":{"es":" ES ","en":" EN "}}""",
                System.Text.Encoding.UTF8, "application/json") };
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
        var client = new ProgramNarrativeClient(http, Options.Create(new AiServiceSettings { InternalApiKey = "clave-simulada" }));
        var response = await client.GenerateAsync(Payload(), CancellationToken.None);
        Assert.Equal("ES", response.NarrativeEs); Assert.Equal("EN", response.NarrativeEn);
        Assert.Null(response.ModelName);
    }

    [Fact]
    public async Task Cliente_SinClave_NoEnviaPerfil()
    {
        var transport = new Transport((_, _) => throw new InvalidOperationException("No debe llamar al proveedor"));
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
        var client = new ProgramNarrativeClient(http, Options.Create(new AiServiceSettings()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAsync(Payload(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Theory]
    [InlineData("""{"narratives":{"es":"ES"}}""")]
    [InlineData("""{"narratives":{"es":"ES","en":null}}""")]
    [InlineData("""{"narratives":{"es":"ES","en":"   "}}""")]
    [InlineData("""{"narratives":null}""")]
    [InlineData("""{"narrative_es":"ES","narrative_en":"EN"}""")]
    public async Task Cliente_RespuestaSinAmbosIdiomas_RechazaNarrativaIncompleta(string body)
    {
        using var http = new HttpClient(new Transport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") })))
            { BaseAddress = new Uri("http://localhost") };
        var client = new ProgramNarrativeClient(http, Options.Create(new AiServiceSettings { InternalApiKey = "test" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAsync(Payload(), CancellationToken.None));
    }

    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return send(request, ct);
        }
    }
}
