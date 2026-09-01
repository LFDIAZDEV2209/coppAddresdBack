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
                [new DetectedFoodDto(
                    "pizza", 0.94, new BoundingBoxDto(120, 80, 300, 180),
                    Segmentation: new SegmentationDto(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
                        44970),
                    Portion: new PortionDto("large", 128, 118, 160, 0.55, "basic_reference"))]);
            return Task.FromResult(SentResult);
        }
    }

    private sealed class FakeNutritionProvider : INutritionProvider
    {
        public FoodNutritionDto? Result { get; set; }

        public Task<FoodNutritionDto?> GetNutritionAsync(string foodKey, CancellationToken ct = default)
            => Task.FromResult(Result);
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