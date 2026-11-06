using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del handler de ingesta de imagen: validación → almacenamiento →
/// envío al Food AI Service, en ese orden.
/// </summary>
public class AnalyzeFoodImageCommandHandlerTests
{
    private sealed class FakeImageStorage : IImageStorage
    {
        public Guid? SavedAnalysisId { get; private set; }
        public string? SavedFileName { get; private set; }

        public Task<string> SaveImageAsync(
            Guid analysisId, string fileName, Stream content, CancellationToken ct = default)
        {
            SavedAnalysisId = analysisId;
            SavedFileName = fileName;
            return Task.FromResult($"foodai/{analysisId:N}.png");
        }

        public Task<string> SaveMaskAsync(
            Guid analysisId, int itemIndex, Stream pngContent, CancellationToken ct = default)
            => Task.FromResult($"foodai/masks/{analysisId:N}/{itemIndex}.png");
    }

    private sealed class FakeFoodAiClient : IFoodAiClient
    {
        public string? NutritionQuery { get; set; }
        public string? NameEn { get; set; }
        public string? Provider { get; set; }
        public EstimatedNutrientsDto? EstimatedNutrients { get; set; }
        public IReadOnlyList<DetectedFoodDto>? TestFoods { get; set; }
        public FoodAiAnalyzeResult? SentResult { get; private set; }
        public Guid? SentAnalysisId { get; private set; }

        public Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default)
            => Task.FromResult(new FoodAiHealthStatus(true, "food-ai-service"));

        public Task<FoodAiAnalyzeResult> SendImageAsync(
            Guid analysisId, Stream image, string fileName, string contentType,
            CancellationToken ct = default)
        {
            SentAnalysisId = analysisId;
            SentResult = new FoodAiAnalyzeResult(
                analysisId.ToString(), "completed", "food-detector-v1", "food-segmenter-v1", "detector-based-v1", 182,
                TestFoods ?? [new DetectedFoodDto(
                    "pizza", 0.94, new BoundingBoxDto(120, 80, 300, 180),
                    Segmentation: new SegmentationDto(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
                        44970),
                    Portion: new PortionDto("large", 128, 118, 160, 0.55, "basic_reference"),
                    NutritionQuery: NutritionQuery, NameEn: NameEn,
                    EstimatedNutrients: EstimatedNutrients)], Provider: Provider);
            return Task.FromResult(SentResult);
        }
    }

    private sealed class FakeNutritionProvider : INutritionProvider
    {
        public FoodNutritionDto? Result { get; set; }
        public string? RequestedKey { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Dictionary<string, FoodNutritionDto>? Results { get; set; }

        public Task<FoodNutritionDto?> GetNutritionAsync(string foodKey, CancellationToken ct = default)
        {
            RequestedKey = foodKey;
            ReceivedToken = ct;
            return Task.FromResult(Results?.GetValueOrDefault(foodKey) ?? Result);
        }
    }

    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class FakeAnalysisRepository : IFoodAnalysisRepository
    {
        public List<CoppAddresd.Domain.Entities.FoodAi.FoodAnalysis> Saved { get; } = [];

        public Task<CoppAddresd.Domain.Entities.FoodAi.FoodAnalysis?> GetByAnalysisIdAsync(
            Guid analysisId, CancellationToken ct = default)
            => Task.FromResult(Saved.FirstOrDefault(a => a.AnalysisId == analysisId));

        public Task<CoppAddresd.Domain.Entities.FoodAi.FoodAnalysis> AddAsync(
            CoppAddresd.Domain.Entities.FoodAi.FoodAnalysis analysis, CancellationToken ct = default)
        {
            if (Saved.All(a => a.AnalysisId != analysis.AnalysisId))
            {
                Saved.Add(analysis);
            }

            return Task.FromResult(analysis);
        }

        public Task AddFeedbackAsync(
            CoppAddresd.Domain.Entities.FoodAi.FoodAnalysisFeedback feedback, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static AnalyzeFoodImageCommandHandler BuildHandler(
        FakeImageStorage? storage = null,
        FakeFoodAiClient? client = null,
        FakeNutritionProvider? nutritionProvider = null,
        FakeAnalysisRepository? analysisRepository = null) => new(
            new ImageFileValidator(Options.Create(new FoodAiSettings())),
            storage ?? new FakeImageStorage(),
            client ?? new FakeFoodAiClient(),
            nutritionProvider ?? new FakeNutritionProvider
            {
                Result = new FoodNutritionDto(
                    "Pizza, cheese, per 100 g", 100m, 266m, 11.39m, 33.33m, 10.4m, 2.3m, 3.6m, 598m,
                    "USDA FoodData Central", "2026-08-27"),
            },
            new NutritionCalculator(),
            analysisRepository ?? new FakeAnalysisRepository(),
            NullLogger<AnalyzeFoodImageCommandHandler>.Instance);

    [Theory]
    [InlineData("chicken and rice", "llm", "Chicken and rice", "chicken and rice")]
    [InlineData(null, null, null, "pizza")]
    public async Task Handle_resuelve_USDA_con_query_o_nombre_y_conserva_idiomas(
        string? query, string? provider, string? nameEn, string expectedKey)
    {
        var client = new FakeFoodAiClient { NutritionQuery = query, NameEn = nameEn, Provider = provider };
        var nutrition = new FakeNutritionProvider();
        var repository = new FakeAnalysisRepository();
        var handler = BuildHandler(client: client, nutritionProvider: nutrition, analysisRepository: repository);
        using var cts = new CancellationTokenSource();
        var result = await handler.Handle(
            new AnalyzeFoodImageCommand(new MemoryStream(Png1x1), "plato.png", "image/png", Png1x1.Length), cts.Token);

        Assert.Equal(expectedKey, nutrition.RequestedKey);
        Assert.Equal(cts.Token, nutrition.ReceivedToken);
        Assert.Equal(provider, result.Provider);
        var food = Assert.Single(result.Foods);
        Assert.Equal(query, food.NutritionQuery);
        Assert.Equal(nameEn, food.NameEn);
        Assert.Equal("pizza", food.Name);
        Assert.Equal("unavailable", food.NutritionResult!.NutritionStatus);
        Assert.Null(result.Summary);
        var savedFood = Assert.Single(Assert.Single(repository.Saved).Items);
        Assert.Equal("pizza", savedFood.Name);
        Assert.Equal(nameEn, savedFood.NameEn);
    }

    [Fact]
    public async Task Handle_imagen_valida_genera_analysis_id_y_envia_al_servicio()
    {
        var storage = new FakeImageStorage();
        var client = new FakeFoodAiClient();
        var handler = BuildHandler(storage, client);
        var stream = new MemoryStream(Png1x1);

        var result = await handler.Handle(
            new AnalyzeFoodImageCommand(stream, "bandeja.png", "image/png", Png1x1.Length),
            CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal("food-detector-v1", result.ModelVersion);
        Assert.Single(result.Foods);
        Assert.Equal("pizza", result.Foods[0].Name);
        Assert.Equal(300, result.Foods[0].BoundingBox.Width);
        Assert.True(Guid.TryParse(result.AnalysisId, out _));
        Assert.Equal(storage.SavedAnalysisId, client.SentAnalysisId);
        Assert.Equal("bandeja.png", storage.SavedFileName);
    }

    [Theory]
    [InlineData(true, 340.48, "usda")]
    [InlineData(false, 192, "llm_estimate")]
    public async Task Handle_prioriza_USDA_y_sin_entrada_usa_estimacion(
        bool usdaHit, double calories, string source)
    {
        var client = new FakeFoodAiClient
        {
            Provider = "llm", NutritionQuery = "pizza cheese",
            EstimatedNutrients = new EstimatedNutrientsDto(150m, 10m, 20m, 3m, 1m),
        };
        var nutrition = new FakeNutritionProvider
        {
            Result = usdaHit ? new FoodNutritionDto("pizza", 100m, 266m, 11m, 33m, 10m, 2m, 3m, 598m, "USDA FoodData Central", "v1") : null,
        };
        var repository = new FakeAnalysisRepository();
        var result = await BuildHandler(client: client, nutritionProvider: nutrition, analysisRepository: repository).Handle(
            new AnalyzeFoodImageCommand(new MemoryStream(Png1x1), "plato.png", "image/png", Png1x1.Length), CancellationToken.None);
        var food = Assert.Single(result.Foods);
        Assert.Equal("available", food.NutritionResult!.NutritionStatus);
        Assert.Equal(source, food.NutritionResult.NutritionSource);
        Assert.Equal((decimal)calories, food.NutritionResult.Nutrition!.Calories);
        Assert.Equal((decimal)calories, result.Summary!.Calories);
        Assert.Equal((decimal)calories, result.Intake!.Calories);
        Assert.NotNull(result.SummaryRange);
        var saved = Assert.Single(Assert.Single(repository.Saved).Items);
        Assert.Equal(usdaHit ? "USDA FoodData Central" : "llm_estimate", saved.Source);
        if (!usdaHit)
        {
            Assert.Null(food.NutritionResult.Nutrition.Sugar);
            Assert.Null(food.NutritionResult.Nutrition.Sodium);
            Assert.Null(saved.Sugar);
            Assert.Null(result.Summary.Sodium);
        }
    }

    [Fact]
    public async Task Handle_totales_mixtos_incluyen_USDA_y_estimados()
    {
        var estimates = new EstimatedNutrientsDto(150m, 10m, 20m, 3m, 1m);
        var client = new FakeFoodAiClient
        {
            Provider = "llm", TestFoods = [
                new DetectedFoodDto("pizza", 0.9, new BoundingBoxDto(0, 0, 0, 0),
                    Portion: new PortionDto("unknown", 100, 80, 120, 0.7, "llm_estimate"), EstimatedNutrients: estimates),
                new DetectedFoodDto("sancocho", 0.8, new BoundingBoxDto(0, 0, 0, 0),
                    Portion: new PortionDto("unknown", 200, 150, 250, 0.7, "llm_estimate"), EstimatedNutrients: estimates),
            ],
        };
        var nutrition = new FakeNutritionProvider
        {
            Results = new() { ["pizza"] = new FoodNutritionDto("pizza", 100m, 266m, 11m, 33m, 10m, 2m, 3m, 598m, "USDA FoodData Central", "v1") },
        };
        var result = await BuildHandler(client: client, nutritionProvider: nutrition).Handle(
            new AnalyzeFoodImageCommand(new MemoryStream(Png1x1), "plato.png", "image/png", Png1x1.Length), CancellationToken.None);
        Assert.Equal("usda", result.Foods[0].NutritionResult!.NutritionSource);
        Assert.Equal("llm_estimate", result.Foods[1].NutritionResult!.NutritionSource);
        Assert.Equal(566m, result.Summary!.Calories);
        Assert.Equal(31m, result.Intake!.ProteinG);
        Assert.Equal(437.8m, result.SummaryRange!.Min.Calories);
        Assert.Equal(694.2m, result.SummaryRange.Max.Calories);
    }

    [Fact]
    public async Task Handle_persiste_analisis_con_snapshot_y_mascara_en_storage()
    {
        var storage = new FakeImageStorage();
        var repository = new FakeAnalysisRepository();
        var handler = BuildHandler(storage, analysisRepository: repository);
        var stream = new MemoryStream(Png1x1);

        var result = await handler.Handle(
            new AnalyzeFoodImageCommand(stream, "bandeja.png", "image/png", Png1x1.Length),
            CancellationToken.None);

        var analysis = Assert.Single(repository.Saved);
        Assert.Equal(result.AnalysisId, analysis.AnalysisId.ToString());
        Assert.Equal("completed", analysis.Status);
        Assert.Equal("food-detector-v1", analysis.DetectorVersion);
        Assert.Equal("food-segmenter-v1", analysis.SegmenterVersion);
        Assert.Equal("detector-based-v1", analysis.ClassifierVersion);
        Assert.Equal("basic_reference", analysis.PortionMethod);

        var item = Assert.Single(analysis.Items);
        Assert.Equal("pizza", item.Name);
        Assert.Equal("large", item.PortionSize);
        Assert.Equal(128, item.EstimatedGrams);
        Assert.Equal("available", item.NutritionStatus);
        Assert.Equal(340.48m, item.Calories);
        Assert.Equal("foodai/masks/" + analysis.AnalysisId.ToString("N") + "/0.png", item.MaskKey);
        Assert.Equal("USDA FoodData Central", item.Source);
        Assert.Equal(340.48m, analysis.SummaryCalories);
    }

    [Fact]
    public async Task Handle_analisis_con_user_id_lo_persiste()
    {
        var repository = new FakeAnalysisRepository();
        var handler = BuildHandler(analysisRepository: repository);
        var userId = Guid.NewGuid();
        var stream = new MemoryStream(Png1x1);

        await handler.Handle(
            new AnalyzeFoodImageCommand(stream, "bandeja.png", "image/png", Png1x1.Length, userId),
            CancellationToken.None);

        var analysis = Assert.Single(repository.Saved);
        Assert.Equal(userId, analysis.UserId);
    }

    [Fact]
    public async Task Handle_imagen_invalida_lanza_InvalidImageException_sin_enviar()
    {
        var storage = new FakeImageStorage();
        var client = new FakeFoodAiClient();
        var handler = BuildHandler(storage, client);
        var texto = System.Text.Encoding.UTF8.GetBytes("no es imagen");

        var ex = await Assert.ThrowsAsync<InvalidImageException>(() =>
            handler.Handle(
                new AnalyzeFoodImageCommand(new MemoryStream(texto), "falso.png", "image/png", texto.Length),
                CancellationToken.None));

        Assert.Equal(ImageFileValidator.CodeCorruptFile, ex.Code);
        Assert.Null(storage.SavedAnalysisId);
        Assert.Null(client.SentAnalysisId);
    }

    [Fact]
    public async Task Handle_archivo_vacio_lanza_InvalidImageException()
    {
        var handler = BuildHandler();

        var ex = await Assert.ThrowsAsync<InvalidImageException>(() =>
            handler.Handle(
                new AnalyzeFoodImageCommand(new MemoryStream(), "vacio.png", "image/png", 0),
                CancellationToken.None));

        Assert.Equal(ImageFileValidator.CodeEmptyFile, ex.Code);
    }
}
