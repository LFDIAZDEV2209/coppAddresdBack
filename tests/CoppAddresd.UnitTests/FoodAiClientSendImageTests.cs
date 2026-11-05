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
    public async Task SendImageAsync_envia_multipart_a_analyze_y_parsea_detecciones()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"analysis_id":"3f3f0f0f-1111-2222-3333-444444444444","status":"completed","model_version":"food-detector-v1","inference_time_ms":182,"classifier_version":"detector-based-v1","seg_model_version":"food-segmenter-v1","foods":[{"name":"pizza","confidence":0.94,"bounding_box":{"x":120,"y":80,"width":300,"height":180},"segmentation":{"mask":"aGVsbG8=","area_pixels":52341},"portion":{"portion_size":"medium","estimated_grams":118,"min_grams":94,"max_grams":142,"confidence":0.55,"method":"basic_reference"}},{"name":"banana","confidence":0.61,"bounding_box":{"x":30,"y":400,"width":90,"height":140}}]}"""),
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
        Assert.Equal("completed", result.Status);
        Assert.Equal("food-detector-v1", result.ModelVersion);
        Assert.Equal(182, result.InferenceTimeMs);
        Assert.Equal("food-segmenter-v1", result.SegModelVersion);
        Assert.Equal("detector-based-v1", result.ClassifierVersion);
        Assert.Equal(2, result.Foods.Count);
        Assert.Equal("pizza", result.Foods[0].Name);
        Assert.Equal(0.94, result.Foods[0].Confidence);
        Assert.Equal(120, result.Foods[0].BoundingBox.X);
        Assert.Equal(80, result.Foods[0].BoundingBox.Y);
        Assert.Equal(300, result.Foods[0].BoundingBox.Width);
        Assert.Equal(180, result.Foods[0].BoundingBox.Height);
        var segmentation = result.Foods[0].Segmentation!;
        Assert.Equal("aGVsbG8=", segmentation.Mask);
        Assert.Equal(52341, segmentation.AreaPixels);
        var portion = result.Foods[0].Portion!;
        Assert.Equal("medium", portion.PortionSize);
        Assert.Equal(118, portion.EstimatedGrams);
        Assert.Equal(0.55, portion.Confidence);
        Assert.Equal("basic_reference", portion.Method);
        Assert.Null(result.Foods[1].Portion);
        Assert.Null(result.Foods[1].Segmentation);
        Assert.Null(result.Provider);
        Assert.Null(result.Foods[0].NutritionQuery);
        Assert.Null(result.Foods[0].NameEn);
        Assert.Null(result.Foods[0].EstimatedNutrients);
    }

    [Fact]
    public async Task SendImageAsync_parsea_nombres_bilingues_y_query_independiente()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"analysis_id":"3f3f0f0f-1111-2222-3333-444444444444","provider":"llm","foods":[{"name":"Arroz con pollo","name_en":"Chicken and rice","nutrition_query":"chicken rice cooked","estimated_nutrients_per_100g":{"calories":150,"protein_g":10,"carbs_g":20,"fat_g":3,"fiber_g":1},"confidence":0.86,"portion":{"portion_size":"unknown","estimated_grams":200,"min_grams":150,"max_grams":250,"confidence":0.7,"method":"llm_estimate"}}]}"""),
        });
        var result = await BuildClient(handler).SendImageAsync(
            Guid.NewGuid(), new MemoryStream(ImageBytes), "plato.png", "image/png");
        Assert.Equal("llm", result.Provider);
        var food = Assert.Single(result.Foods);
        Assert.Equal("Arroz con pollo", food.Name);
        Assert.Equal("Chicken and rice", food.NameEn);
        Assert.Equal("chicken rice cooked", food.NutritionQuery);
        Assert.Equal("llm_estimate", food.Portion!.Method);
        Assert.Equal(new EstimatedNutrientsDto(150m, 10m, 20m, 3m, 1m), food.EstimatedNutrients);
        Assert.Null(food.Segmentation);
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
