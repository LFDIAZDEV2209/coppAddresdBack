using System.Net;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.Chat;

/// <summary>
/// Cliente <c>AiServiceClient.SendFeedbackAsync</c> (Fase 9): contrato
/// snake_case hacia <c>POST /api/v1/chat/feedback</c> con
/// <c>X-Internal-Key</c> y mapeo de la respuesta de adaptive memory.
/// </summary>
public sealed class ChatFeedbackClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public sealed record Captured(
            string Method,
            string Path,
            IReadOnlyDictionary<string, string> Headers,
            string Body
        );

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(
                new Captured(
                    request.Method.Method,
                    request.RequestUri!.AbsolutePath,
                    request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    body
                )
            );
            return responder(request);
        }
    }

    private static AiServiceClient BuildClient(StubHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(
                new AiServiceSettings
                {
                    BaseUrl = "http://ai.test",
                    InternalApiKey = "secret-internal-key",
                    TimeoutSeconds = 30,
                }
            ),
            NullLogger<AiServiceClient>.Instance
        );

    [Fact]
    public async Task SendFeedbackAsync_EnviaContratoSnakeCaseConInternalKey()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","rating":5,"experience_saved":true,"outcome":"success"}"""
            ),
        });
        var client = BuildClient(handler);

        var result = await client.SendFeedbackAsync(
            new ChatFeedbackRequestDto("e1", "t1", 5, "Muy útil"),
            "paciente-123"
        );

        var request = handler.Requests.Single();
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/v1/chat/feedback", request.Path);
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        Assert.Equal("e1", root.GetProperty("execution_id").GetString());
        Assert.Equal("t1", root.GetProperty("thread_id").GetString());
        Assert.Equal(5, root.GetProperty("rating").GetInt32());
        Assert.Equal("Muy útil", root.GetProperty("comment").GetString());
        Assert.Equal("paciente-123", root.GetProperty("user_id").GetString());

        Assert.Equal("t1", result.ThreadId);
        Assert.Equal(5, result.Rating);
        Assert.True(result.ExperienceSaved);
        Assert.Equal("success", result.Outcome);
    }

    [Fact]
    public async Task SendFeedbackAsync_ComentarioNull_NoSeEnvia()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","rating":4,"experience_saved":false}"""
            ),
        });
        var client = BuildClient(handler);

        var result = await client.SendFeedbackAsync(
            new ChatFeedbackRequestDto("e1", "t1", 4, null),
            "paciente-123"
        );

        Assert.DoesNotContain("comment", handler.Requests.Single().Body);
        Assert.False(result.ExperienceSaved);
        Assert.Null(result.Outcome);
    }

    [Fact]
    public async Task SendFeedbackAsync_Error_MapeaAAiServiceException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("ai-service caído"),
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.SendFeedbackAsync(
                new ChatFeedbackRequestDto("e1", "t1", 5, null),
                "paciente-123"
            )
        );

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("ai-service caído", ex.Detail);
    }
}
