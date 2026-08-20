using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del proxy de ejecuciones admin: header X-Internal-Key (Fase 1),
/// parsing snake_case y fallo → lista vacía (nunca 500 al cliente).
/// </summary>
public class AgentExecutionsQueryServiceTests
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

    private static AgentExecutionsQueryService BuildClient(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://ai.test") },
        Options.Create(new AiServiceSettings
        {
            BaseUrl = "http://ai.test",
            InternalApiKey = "secret-internal-key",
            TimeoutSeconds = 30,
        }),
        NullLogger<AgentExecutionsQueryService>.Instance);

    [Fact]
    public async Task ListAsync_envia_key_y_parsa_pagina()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"total":1,"items":[{"id":"e1","agent_type_id":"base","status":"completado","created_at":"2026-01-01T00:00:00Z"}]}"""),
        });
        var client = BuildClient(handler);

        var result = await client.ListAsync(new AgentExecutionQueryOptions());

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/admin/executions", request.Path);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("e1", result.Items[0].Id);
        Assert.Equal("completado", result.Items[0].Status);
    }

    [Fact]
    public async Task ListAsync_error_devuelve_lista_vacia()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom"),
        });
        var client = BuildClient(handler);

        var result = await client.ListAsync(new AgentExecutionQueryOptions());

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAsync_404_devuelve_null()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(handler);

        var result = await client.GetAsync("no-existe");

        Assert.Null(result);
    }
}
