using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del cliente del AI Service: contrato snake_case, header X-Internal-Key
/// (Fase 1), parsing de respuesta y mapeo de errores tipados.
/// </summary>
public class AiServiceClientTests
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

    private static AiServiceClient BuildClient(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new AiServiceSettings
        {
            BaseUrl = "http://ai.test",
            InternalApiKey = "secret-internal-key",
            TimeoutSeconds = 30,
        }),
        NullLogger<AiServiceClient>.Instance);

    [Fact]
    public async Task ChatAsync_envia_internal_key_y_contrato_snake_case()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"answer":"hola","thread_id":"t1","execution_id":"e1","agent":"base"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.ChatAsync(new ChatRequest(
            Message: "hola",
            Agent: null,
            ThreadId: "t1",
            AgentTypeId: "guid-1",
            UserId: "user-1"));

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/chat", request.Path);

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        Assert.Equal("hola", root.GetProperty("message").GetString());
        Assert.Equal("base", root.GetProperty("agent").GetString()); // default si null
        Assert.Equal("t1", root.GetProperty("thread_id").GetString());
        Assert.Equal("guid-1", root.GetProperty("agent_type_id").GetString());
        Assert.Equal("user-1", root.GetProperty("user_id").GetString());

        Assert.Equal("hola", result.Reply);
        Assert.Equal("t1", result.ThreadId);
        Assert.Equal("e1", result.ExecutionId);
        Assert.Equal("base", result.Agent);
    }

    [Fact]
    public async Task ChatAsync_campos_null_no_se_envian()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"answer":"x","thread_id":"t"}"""),
        });
        var client = BuildClient(handler);

        await client.ChatAsync(new ChatRequest(Message: "hola"));

        var request = handler.Requests.Single();
        Assert.DoesNotContain("agent_type_id", request.Body);
        Assert.DoesNotContain("user_id", request.Body);
    }

    [Fact]
    public async Task ChatAsync_error_mapea_a_AiServiceException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("Demasiadas solicitudes"),
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(new ChatRequest(Message: "hola")));

        Assert.Equal(429, ex.StatusCode);
        Assert.Contains("Demasiadas", ex.Detail);
    }

    [Fact]
    public async Task StreamRawAsync_envia_internal_key()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("event: done\ndata: {\"thread_id\":\"t1\"}\n\n"),
        });
        var client = BuildClient(handler);

        var chunks = new List<StreamChatChunk>();
        await foreach (var chunk in client.StreamRawAsync(new ChatRequest(Message: "hola")))
            chunks.Add(chunk);

        Assert.NotEmpty(chunks);
        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/chat/stream", request.Path);
    }
}
