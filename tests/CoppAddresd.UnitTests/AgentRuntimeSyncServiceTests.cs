using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del sincronizador de configuración de agentes: header X-Internal-Key,
/// config enviada como objeto (no string) y manejo de errores de ingestión.
/// </summary>
public class AgentRuntimeSyncServiceTests
{
    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public sealed record Captured(
            string Method,
            string Path,
            IReadOnlyDictionary<string, string> Headers,
            string Body);

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new Captured(
                request.Method.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                body));
            return responder(request);
        }
    }

    private static AgentRuntimeSyncService BuildClient(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://ai.test") },
        Options.Create(new AiServiceSettings
        {
            BaseUrl = "http://ai.test",
            InternalApiKey = "secret-internal-key",
            TimeoutSeconds = 30,
        }),
        NullLogger<AgentRuntimeSyncService>.Instance);

    [Fact]
    public async Task SyncAgentConfigAsync_envia_key_y_config_como_objeto()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(handler);

        var configJson = """{"system_prompt":"p","memory_config":{"enabled":true}}""";
        await client.SyncAgentConfigAsync(new AgentRuntimeConfigPayload(
            Guid.NewGuid(), Guid.NewGuid(), 1, "Agent", "desc", "gen", "icon", configJson));

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/internal/agents/sync-config", request.Path);

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        // La config debe viajar como objeto JSON, no como string anidado.
        Assert.Equal(JsonValueKind.Object, root.GetProperty("config").ValueKind);
        Assert.Equal("p", root.GetProperty("config").GetProperty("system_prompt").GetString());
    }

    [Fact]
    public async Task IngestDocumentAsync_exito_parsa_resultado()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"indexado","chunks_created":4,"replaced_chunks":0}"""),
        });
        var client = BuildClient(handler);

        var result = await client.IngestDocumentAsync(new AgentDocumentIngestPayload(
            Guid.NewGuid(), Guid.NewGuid(), "doc.md", "YWNhYmM="));

        Assert.Equal("indexado", result.Status);
        Assert.Equal(4, result.ChunksCreated);
    }

    [Fact]
    public async Task IngestDocumentAsync_rechazo_devuelve_error()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad"),
        });
        var client = BuildClient(handler);

        var result = await client.IngestDocumentAsync(new AgentDocumentIngestPayload(
            Guid.NewGuid(), Guid.NewGuid(), "doc.md", "YWNhYmM="));

        Assert.Equal("error", result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
