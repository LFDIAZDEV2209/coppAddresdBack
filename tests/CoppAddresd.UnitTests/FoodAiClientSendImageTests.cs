using System.Net;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests de envío de imagen al Food AI Service: contrato multipart
/// (part image + campo analysis_id), parsing de respuesta y errores tipados.
/// </summary>
public class FoodAiClientSendImageTests
{
    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public sealed record Captured(
            string Method,
            string Path,
            string MultipartBoundary,
            string Body);

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var boundary = request.Content?.Headers.ContentType?.Parameters
                .FirstOrDefault(p => p.Name == "boundary")?.Value ?? "";
            Requests.Add(new Captured(request.Method.Method, request.RequestUri!.AbsolutePath, boundary, body));
            return responder(request);
        }
    }

    private static FoodAiClient BuildClient(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new FoodAiSettings { BaseUrl = "http://foodai.test", TimeoutSeconds = 5 }),
        NullLogger<FoodAiClient>.Instance);

    private static readonly byte[] ImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task SendImageAsync_envia_multipart_a_analyze_y_parsea_respuesta()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"analysis_id":"3f3f0f0f-1111-2222-3333-444444444444","status":"received"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.SendImageAsync(
            Guid.Parse("3f3f0f0f-1111-2222-3333-444444444444"),
            new MemoryStream(ImageBytes),
            "plato.png",
            "image/png");

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post.Method, request.Method);
        Assert.Equal("/analyze", request.Path);
        Assert.Contains("name=analysis_id", request.Body, StringComparison.Ordinal);
        Assert.Contains("3f3f0f0f-1111-2222-3333-444444444444", request.Body, StringComparison.Ordinal);
        Assert.Contains("name=image; filename=plato.png", request.Body, StringComparison.Ordinal);
        Assert.Contains("Content-Type: image/png", request.Body, StringComparison.Ordinal);

        Assert.Equal("3f3f0f0f-1111-2222-3333-444444444444", result.AnalysisId);
        Assert.Equal("received", result.Status);
    }

    [Fact]
    public async Task SendImageAsync_error_del_servicio_mapea_a_FoodAiException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":"INVALID_IMAGE","message":"bad"}}"""),
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<FoodAiException>(() =>
            client.SendImageAsync(Guid.NewGuid(), new MemoryStream(ImageBytes), "x.png", "image/png"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("INVALID_IMAGE", ex.Detail);
    }

    [Fact]
    public async Task SendImageAsync_respuesta_invalida_mapea_a_FoodAiException_502()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("no-json"),
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<FoodAiException>(() =>
            client.SendImageAsync(Guid.NewGuid(), new MemoryStream(ImageBytes), "x.png", "image/png"));

        Assert.Equal(502, ex.StatusCode);
    }
}