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

    [Fact]
    public async Task GetGraphAsync_envia_key_y_parsa_descriptor()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "agent_type_id": "ag-1",
                  "source": "runtime",
                  "version_id": "v-3",
                  "nodes": [
                    {"id": "guardrails", "label": "Guardrails", "kind": "guard", "description": "d", "meta": {}},
                    {"id": "agent", "label": "Agente (LLM)", "kind": "llm", "description": null, "meta": {}}
                  ],
                  "edges": [
                    {"source": "guardrails", "target": "agent", "kind": "conditional", "label": "seguro"}
                  ],
                  "config": {
                    "provider": "anthropic",
                    "model": "claude-sonnet-4",
                    "temperature": 0.3,
                    "max_tokens": 1024,
                    "tools": ["calculate", "retrieve_knowledge"],
                    "rag": {"enabled": true, "knowledge_base_count": 2, "top_k": 4},
                    "memory": {"enabled": true, "categories": ["clinico"]},
                    "max_tool_calls": 5,
                    "recursion_limit": 18
                  }
                }
                """),
        });
        var client = BuildClient(handler);

        var result = await client.GetGraphAsync("ag-1");

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/internal/agents/ag-1/graph", request.Path);
        Assert.NotNull(result);
        Assert.Equal("runtime", result!.Source);
        Assert.Equal("v-3", result.VersionId);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal("llm", result.Nodes[1].Kind);
        Assert.Single(result.Edges);
        Assert.Equal("conditional", result.Edges[0].Kind);
        Assert.True(result.Config.Rag.Enabled);
        Assert.Equal(2, result.Config.Rag.KnowledgeBaseCount);
        Assert.True(result.Config.Memory.Enabled);
        Assert.Equal(5, result.Config.MaxToolCalls);
        Assert.Contains("retrieve_knowledge", result.Config.Tools);
    }

    [Fact]
    public async Task GetGraphAsync_404_devuelve_null()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(handler);

        var result = await client.GetGraphAsync("sin-grafo");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGraphAsync_error_devuelve_null()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom"),
        });
        var client = BuildClient(handler);

        var result = await client.GetGraphAsync("ag-2");

        Assert.Null(result);
    }
}
