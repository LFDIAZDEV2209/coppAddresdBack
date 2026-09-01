using System.Net;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del cliente del Food AI Service: contrato de salud, manejo de
/// respuestas no exitosas y fallos de red sin excepciones.
/// </summary>
public class FoodAiClientTests
{
    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public sealed record Captured(
            string Method,
            string Path);

        public List<Captured> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new Captured(request.Method.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(responder(request));
        }
    }

    private static FoodAiClient BuildClient(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new FoodAiSettings
        {
            BaseUrl = "http://foodai.test",
            TimeoutSeconds = 5,
        }),
        NullLogger<FoodAiClient>.Instance);

    [Fact]
    public async Task GetHealthAsync_healthy_cuando_el_servicio_responde()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"status":"healthy","service":"food-ai-service","version":"0.1.0"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.GetHealthAsync();

        Assert.True(result.IsHealthy, $"Detail: {result.Detail}");
        Assert.Equal("food-ai-service", result.Detail);
        Assert.Equal("/health", handler.Requests.Single().Path);
    }

    [Fact]
    public async Task GetHealthAsync_unhealthy_con_status_http_no_exitoso()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = BuildClient(handler);

        var result = await client.GetHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Equal("HTTP 503", result.Detail);
    }

    [Fact]
    public async Task GetHealthAsync_unhealthy_si_el_servicio_reporta_degradado()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"degraded","service":"food-ai-service"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.GetHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Equal("degraded", result.Detail);
    }

    [Fact]
    public async Task GetHealthAsync_unhealthy_sin_excepcion_cuando_la_red_falla()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var client = BuildClient(handler);

        var result = await client.GetHealthAsync();

        Assert.False(result.IsHealthy);
        Assert.Contains("HttpRequestException", result.Detail);
    }
}