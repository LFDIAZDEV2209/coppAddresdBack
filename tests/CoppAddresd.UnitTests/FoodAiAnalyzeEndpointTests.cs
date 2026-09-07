using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests de integración del endpoint POST /api/v1/foodai/analyze contra el
/// pipeline HTTP real (multipart binding, validación, mapeo de errores),
/// con IFoodAiClient e IImageStorage sustituidos por stubs.
/// </summary>
public class FoodAiAnalyzeEndpointTests
    : IClassFixture<WebApplicationFactory<CoppAddresd.Api.ApiEntryPoint>>
{
    private sealed class StubFoodAiClient : IFoodAiClient
    {
        public FoodAiAnalyzeResult? LastSend { get; private set; }
        public Guid? LastAnalysisId { get; private set; }
        public string? LastFileName { get; private set; }

        public Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default) =>
            Task.FromResult(new FoodAiHealthStatus(true, "food-ai-service"));

        public Task<FoodAiAnalyzeResult> SendImageAsync(
            Guid analysisId,
            Stream image,
            string fileName,
            string contentType,
            CancellationToken ct = default
        )
        {
            LastAnalysisId = analysisId;
            LastFileName = fileName;
            LastSend = new FoodAiAnalyzeResult(
                analysisId.ToString(),
                "completed",
                "food-detector-v1",
                "food-segmenter-v1",
                "detector-based-v1",
                182,
                [new DetectedFoodDto("pizza", 0.94, new BoundingBoxDto(120, 80, 300, 180))]
            );
            return Task.FromResult(LastSend);
        }
    }

    private sealed class StubImageStorage : IImageStorage
    {
        public Task<string> SaveImageAsync(
            Guid analysisId,
            string fileName,
            Stream content,
            CancellationToken ct = default
        ) => Task.FromResult($"foodai/{analysisId:N}.png");

        public Task<string> SaveMaskAsync(
            Guid analysisId,
            int itemIndex,
            Stream pngContent,
            CancellationToken ct = default
        ) => Task.FromResult($"foodai/masks/{analysisId:N}/{itemIndex}.png");
    }

    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
    );

    private sealed class StubNutritionProvider : INutritionProvider
    {
        public FoodNutritionDto? Result { get; set; }

        public Task<FoodNutritionDto?> GetNutritionAsync(
            string foodKey,
            CancellationToken ct = default
        ) => Task.FromResult(Result);
    }

    private readonly WebApplicationFactory<CoppAddresd.Api.ApiEntryPoint> _factory;

    public FoodAiAnalyzeEndpointTests(WebApplicationFactory<CoppAddresd.Api.ApiEntryPoint> factory)
    {
        // Entorno "Testing" (mismo patrón que ProgramApiHost): evita cargar
        // appsettings.Development.json, que abriría el file sink de Serilog
        // (logs/api-.log) dentro del bin de los tests. La config necesaria
        // para el arranque del host (DefaultConnection, Jwt, Storage) vive en
        // appsettings.Testing.json (gitignoreado como el resto de appsettings).
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Storage:SignatureKey", "test-signature-key-32-chars-long-for-ci");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IFoodAiClient, StubFoodAiClient>();
                services.AddScoped<IImageStorage, StubImageStorage>();
            });
        });
    }

    [Fact]
    public async Task Analyze_imagen_valida_responde_200_con_analysis_id_y_status_received()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(Png1x1);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", "bandeja.png");

        var response = await client.PostAsync("/api/v1/foodai/analyze", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FoodAiAnalyzeEndpointTestsResponse>();
        Assert.NotNull(body);
        Assert.True(Guid.TryParse(body.AnalysisId, out _));
        Assert.Equal("completed", body.Status);
        Assert.Equal("food-detector-v1", body.ModelVersion);
        Assert.Single(body.Foods);
        Assert.Equal("pizza", body.Foods[0].Name);
        Assert.Equal(0.94, body.Foods[0].Confidence);
        Assert.Equal(120, body.Foods[0].BoundingBox.X);
        Assert.Equal(300, body.Foods[0].BoundingBox.Width);
    }

    [Fact]
    public async Task Analyze_archivo_vacio_responde_400_EMPTY_FILE()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", "vacio.png");

        var response = await client.PostAsync("/api/v1/foodai/analyze", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("EMPTY_FILE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Analyze_archivo_corrupto_responde_400_CORRUPT_FILE()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(Encoding.UTF8.GetBytes("no soy una imagen"));
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", "falso.png");

        var response = await client.PostAsync("/api/v1/foodai/analyze", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CORRUPT_FILE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nutrition_encuentra_alimento_devuelve_200_con_valores_por_100g()
    {
        var stubProvider = new StubNutritionProvider
        {
            Result = new FoodNutritionDto(
                "Banana, raw",
                100m,
                89m,
                1.09m,
                22.84m,
                0.33m,
                2.6m,
                12.23m,
                1m,
                "USDA FoodData Central",
                "2026-08-27"
            ),
        };
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Storage:SignatureKey", "test-signature-key-32-chars-long-for-ci");
            builder.ConfigureServices(services =>
                services.AddScoped<INutritionProvider>(_ => stubProvider)
            );
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foodai/nutrition/banana");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<NutritionResponseJson>();
        Assert.NotNull(body);
        Assert.Equal("Banana, raw", body.FoodName);
        Assert.Equal(89m, body.Calories);
        Assert.Equal(100m, body.ServingGrams);
    }

    [Fact]
    public async Task Nutrition_alimento_inexistente_responde_404_FOOD_NOT_FOUND()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Storage:SignatureKey", "test-signature-key-32-chars-long-for-ci");
            builder.ConfigureServices(services =>
                services.AddScoped<INutritionProvider>(_ => new StubNutritionProvider
                {
                    Result = null,
                })
            );
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foodai/nutrition/arepa");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("FOOD_NOT_FOUND", await response.Content.ReadAsStringAsync());
    }

    private sealed class NutritionResponseJson
    {
        public string? FoodName { get; set; }
        public decimal ServingGrams { get; set; }
        public decimal Calories { get; set; }
    }

    private sealed class FoodAiAnalyzeEndpointTestsResponse
    {
        public string? AnalysisId { get; set; }
        public string? Status { get; set; }
        public string? ModelVersion { get; set; }
        public List<FoodJson> Foods { get; set; } = [];

        public sealed class FoodJson
        {
            public string? Name { get; set; }
            public double Confidence { get; set; }
            public BoxJson? BoundingBox { get; set; }
        }

        public sealed class BoxJson
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}
