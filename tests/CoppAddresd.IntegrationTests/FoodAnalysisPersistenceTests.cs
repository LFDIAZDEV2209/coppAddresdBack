using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Tests de persistencia de análisis y feedback contra PostgreSQL real
/// (schema foodai). Requiere COP_TEST_DB_CONNECTION y la migración
/// AddFoodAiAnalysis aplicada.
/// </summary>
[Collection("foodai-nutrition")]
public sealed class FoodAnalysisPersistenceTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private readonly Guid _analysisId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private bool _skipped;

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (_skipped)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private AppDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static FoodAnalysis BuildAnalysis(Guid analysisId, Guid? userId) => new()
    {
        AnalysisId = analysisId,
        Status = "completed",
        ImageKey = $"foodai/{analysisId:N}.png",
        UserId = userId,
        DetectorVersion = "food-detector-v1",
        SegmenterVersion = "food-segmenter-v1",
        ClassifierVersion = "detector-based-v1",
        PortionMethod = "basic_reference",
        SummaryCalories = 340.48m,
        SummaryProtein = 14.58m,
        SummaryCarbohydrates = 42.66m,
        SummaryFat = 13.31m,
        SummarySodium = 765.44m,
        Source = "USDA FoodData Central",
        SourceVersion = "2026-08-27",
        Items =
        {
            new FoodAnalysisItem
            {
                ItemIndex = 0,
                Name = "pizza",
                DetectionConfidence = 0.9253,
                BboxX = 7,
                BboxY = 14,
                BboxWidth = 318,
                BboxHeight = 216,
                MaskKey = $"foodai/masks/{analysisId:N}/0.png",
                MaskAreaPixels = 44970,
                PortionSize = "large",
                EstimatedGrams = 128,
                MinGrams = 118,
                MaxGrams = 160,
                PortionConfidence = 0.55,
                PortionMethod = "basic_reference",
                NutritionStatus = "available",
                Calories = 340.48m,
                Carbohydrates = 42.66m,
                Protein = 14.58m,
                Fat = 13.31m,
                Sodium = 765.44m,
                Source = "USDA FoodData Central",
                SourceVersion = "2026-08-27",
            },
        },
    };

    [Fact]
    public async Task Persistir_y_recuperar_analisis_con_items_y_snapshot()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        var repository = new FoodAnalysisRepository(db);

        var saved = await repository.AddAsync(BuildAnalysis(_analysisId, _userId));
        var loaded = await repository.GetByAnalysisIdAsync(_analysisId);

        Assert.NotNull(loaded);
        Assert.Equal(_analysisId, loaded.AnalysisId);
        Assert.Equal(_userId, loaded.UserId);
        Assert.Equal("completed", loaded.Status);
        Assert.Equal("food-detector-v1", loaded.DetectorVersion);
        Assert.Equal(340.48m, loaded.SummaryCalories);
        var item = Assert.Single(loaded.Items);
        Assert.Equal("pizza", item.Name);
        Assert.Equal(128, item.EstimatedGrams);
        Assert.Equal(340.48m, item.Calories);
        Assert.Equal("foodai/masks/" + _analysisId.ToString("N") + "/0.png", item.MaskKey);
    }

    [Fact]
    public async Task AddAsync_es_idempotente_por_analysis_id()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        var repository = new FoodAnalysisRepository(db);

        await repository.AddAsync(BuildAnalysis(_analysisId, _userId));
        await repository.AddAsync(BuildAnalysis(_analysisId, _userId));

        var count = await db.FoodAnalyses.CountAsync(a => a.AnalysisId == _analysisId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Feedback_preserva_original_y_corregido()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        var repository = new FoodAnalysisRepository(db);
        await repository.AddAsync(BuildAnalysis(_analysisId, _userId));

        var loaded = await repository.GetByAnalysisIdAsync(_analysisId);
        Assert.NotNull(loaded);

        await repository.AddFeedbackAsync(new FoodAnalysisFeedback
        {
            AnalysisId = loaded.Id,
            ItemId = loaded.Items.OrderBy(i => i.ItemIndex).First().Id,
            UserId = _userId,
            FeedbackType = "PORTION_WRONG",
            OriginalFood = "pizza",
            CorrectedFood = "lasaña",
            OriginalGrams = 128,
            CorrectedGrams = 200,
        });

        var reloaded = await repository.GetByAnalysisIdAsync(_analysisId);
        var feedback = Assert.Single(reloaded!.Feedbacks);
        Assert.Equal("PORTION_WRONG", feedback.FeedbackType);
        Assert.Equal(128, feedback.OriginalGrams);
        Assert.Equal(200, feedback.CorrectedGrams);
        Assert.Equal("pizza", feedback.OriginalFood);
        Assert.Equal("lasaña", feedback.CorrectedFood);
        Assert.Equal(_userId, feedback.UserId);

        // El valor original del análisis NO se sobrescribió.
        Assert.Equal(128, reloaded.Items.OrderBy(i => i.ItemIndex).First().EstimatedGrams);
    }

    [Fact]
    public async Task Analisis_sin_items_y_analisis_con_varios_items()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        var repository = new FoodAnalysisRepository(db);

        var empty = new FoodAnalysis
        {
            AnalysisId = Guid.NewGuid(),
            Status = "completed",
            DetectorVersion = "v1",
            SegmenterVersion = "v1",
            ClassifierVersion = "v1",
            PortionMethod = "basic_reference",
        };
        await repository.AddAsync(empty);
        var loadedEmpty = await repository.GetByAnalysisIdAsync(empty.AnalysisId);
        Assert.NotNull(loadedEmpty);
        Assert.Empty(loadedEmpty.Items);

        var multi = BuildAnalysis(Guid.NewGuid(), null);
        multi.Items.Add(new FoodAnalysisItem
        {
            ItemIndex = 1,
            Name = "banana",
            DetectionConfidence = 0.89,
            BboxX = 1,
            BboxY = 1,
            BboxWidth = 10,
            BboxHeight = 10,
            PortionSize = "medium",
            EstimatedGrams = 118,
            NutritionStatus = "available",
            Calories = 105.02m,
        });
        await repository.AddAsync(multi);
        var loadedMulti = await repository.GetByAnalysisIdAsync(multi.AnalysisId);
        Assert.NotNull(loadedMulti);
        Assert.Equal(2, loadedMulti.Items.Count);
        var orderedItems = loadedMulti.Items.OrderBy(i => i.ItemIndex).ToList();
        Assert.Equal(0, orderedItems[0].ItemIndex);
        Assert.Equal(1, orderedItems[1].ItemIndex);
    }
}