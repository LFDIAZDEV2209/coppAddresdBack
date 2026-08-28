using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.FoodAi;

public class AnalyzeFoodImageCommandHandler
    : IRequestHandler<AnalyzeFoodImageCommand, AnalyzeFoodImageResult>
{
    private readonly ImageFileValidator _validator;
    private readonly IImageStorage _imageStorage;
    private readonly IFoodAiClient _foodAiClient;
    private readonly INutritionProvider _nutritionProvider;
    private readonly INutritionCalculator _nutritionCalculator;
    private readonly ILogger<AnalyzeFoodImageCommandHandler> _logger;

    public AnalyzeFoodImageCommandHandler(
        ImageFileValidator validator,
        IImageStorage imageStorage,
        IFoodAiClient foodAiClient,
        INutritionProvider nutritionProvider,
        INutritionCalculator nutritionCalculator,
        ILogger<AnalyzeFoodImageCommandHandler> logger)
    {
        _validator = validator;
        _imageStorage = imageStorage;
        _foodAiClient = foodAiClient;
        _nutritionProvider = nutritionProvider;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
    }

    public async Task<AnalyzeFoodImageResult> Handle(
        AnalyzeFoodImageCommand request,
        CancellationToken ct)
    {
        var validation = _validator.Validate(
            request.ImageStream, request.FileName, request.ContentType, request.Length);
        if (!validation.IsValid)
        {
            _logger.LogInformation(
                "Imagen rechazada en ingesta: {Code} ({File})",
                validation.ErrorCode, request.FileName);
            throw new InvalidImageException(validation.ErrorCode!, validation.ErrorMessage!);
        }

        var analysisId = Guid.NewGuid();
        _logger.LogInformation(
            "Ingesta de imagen: AnalysisId={AnalysisId}, File={File}, Bytes={Bytes}",
            analysisId, request.FileName, request.Length);

        request.ImageStream.Position = 0;
        await _imageStorage.SaveImageAsync(analysisId, request.FileName, request.ImageStream, ct);

        request.ImageStream.Position = 0;
        var result = await _foodAiClient.SendImageAsync(
            analysisId, request.ImageStream, request.FileName, request.ContentType, ct);

        // Nutrición: única fuente en PostgreSQL (.NET). Por alimento →
        // provider (100 g) → calculadora (gramos estimados) → rango y totales.
        var foods = new List<DetectedFoodDto>(result.Foods.Count);
        foreach (var food in result.Foods)
        {
            var nutritionResult = await ResolveNutritionAsync(food, ct);
            foods.Add(food with { NutritionResult = nutritionResult });
        }

        var summary = ComputeSummary(foods);

        return new AnalyzeFoodImageResult(
            analysisId.ToString(), result.Status, result.ModelVersion, result.SegModelVersion,
            result.ClassifierVersion, result.InferenceTimeMs, foods,
            summary.Summary, summary.SummaryRange);
    }

    private async Task<FoodNutritionResult?> ResolveNutritionAsync(
        DetectedFoodDto food,
        CancellationToken ct)
    {
        if (food.Portion is null || food.Portion.EstimatedGrams is null)
        {
            return new FoodNutritionResult(null, null, "portion_unavailable");
        }

        var per100g = await _nutritionProvider.GetNutritionAsync(food.Name, ct);
        if (per100g is null)
        {
            return new FoodNutritionResult(null, null, "unavailable");
        }

        return _nutritionCalculator.Calculate(
            per100g, food.Portion.EstimatedGrams, food.Portion.MinGrams, food.Portion.MaxGrams);
    }

    private static (NutritionValueDto? Summary, NutritionRangeDto? SummaryRange) ComputeSummary(
        IReadOnlyList<DetectedFoodDto> foods)
    {
        // Suma de alimentos con nutrición disponible; cada detección contribuye
        // una sola vez (el pipeline entrega una entrada por alimento).
        var available = foods
            .Where(f => f.NutritionResult?.NutritionStatus == "available" && f.NutritionResult.Nutrition is not null)
            .ToList();
        if (available.Count == 0)
        {
            return (null, null);
        }

        decimal Sum(Func<NutritionValueDto, decimal> selector) =>
            Math.Round(available.Sum(f => selector(f.NutritionResult!.Nutrition!)), 2, MidpointRounding.AwayFromZero);

        var summary = new NutritionValueDto(
            Calories: Sum(n => n.Calories),
            Protein: Sum(n => n.Protein),
            Carbohydrates: Sum(n => n.Carbohydrates),
            Fat: Sum(n => n.Fat),
            Fiber: Sum(n => n.Fiber),
            Sugar: Sum(n => n.Sugar),
            Sodium: Sum(n => n.Sodium));

        NutritionValueDto? SumRange(Func<NutritionValueDto, decimal> selector, Func<FoodNutritionResult, NutritionValueDto> pick)
        {
            var values = available
                .Where(f => f.NutritionResult!.NutritionRange is not null)
                .Select(f => pick(f.NutritionResult!))
                .ToList();
            if (values.Count == 0)
            {
                return null;
            }

            return new NutritionValueDto(
                Calories: Math.Round(values.Sum(selector), 2, MidpointRounding.AwayFromZero),
                Protein: Math.Round(values.Sum(v => v.Protein), 2, MidpointRounding.AwayFromZero),
                Carbohydrates: Math.Round(values.Sum(v => v.Carbohydrates), 2, MidpointRounding.AwayFromZero),
                Fat: Math.Round(values.Sum(v => v.Fat), 2, MidpointRounding.AwayFromZero),
                Fiber: Math.Round(values.Sum(v => v.Fiber), 2, MidpointRounding.AwayFromZero),
                Sugar: Math.Round(values.Sum(v => v.Sugar), 2, MidpointRounding.AwayFromZero),
                Sodium: Math.Round(values.Sum(v => v.Sodium), 2, MidpointRounding.AwayFromZero));
        }

        var min = SumRange(n => n.Calories, r => r.NutritionRange!.Min);
        var max = SumRange(n => n.Calories, r => r.NutritionRange!.Max);
        var summaryRange = min is not null && max is not null
            ? new NutritionRangeDto(min, max)
            : null;

        return (summary, summaryRange);
    }
}