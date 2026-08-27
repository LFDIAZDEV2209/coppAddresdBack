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
                analysisId.ToString(), "completed", "food-detector-v1", "food-segmenter-v1", 182,
                [new DetectedFoodDto("pizza", 0.94, new BoundingBoxDto(120, 80, 300, 180))]);
            return Task.FromResult(SentResult);
        }
    }

    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static AnalyzeFoodImageCommandHandler BuildHandler(
        FakeImageStorage? storage = null,
        FakeFoodAiClient? client = null) => new(
            new ImageFileValidator(Options.Create(new FoodAiSettings())),
            storage ?? new FakeImageStorage(),
            client ?? new FakeFoodAiClient(),
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