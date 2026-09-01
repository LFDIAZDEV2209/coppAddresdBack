using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
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
    private readonly IFoodAnalysisRepository _analysisRepository;
    private readonly ILogger<AnalyzeFoodImageCommandHandler> _logger;

    public AnalyzeFoodImageCommandHandler(
        ImageFileValidator validator,
        IImageStorage imageStorage,
        IFoodAiClient foodAiClient,
        INutritionProvider nutritionProvider,
        INutritionCalculator nutritionCalculator,
        IFoodAnalysisRepository analysisRepository,
        ILogger<AnalyzeFoodImageCommandHandler> logger)
    {
        _validator = validator;
        _imageStorage = imageStorage;
        _foodAiClient = foodAiClient;
        _nutritionProvider = nutritionProvider;
        _nutritionCalculator = nutritionCalculator;
        _analysisRepository = analysisRepository;
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
        var imageKey = await _imageStorage.SaveImageAsync(analysisId, request.FileName, request.ImageStream, ct);

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

        // Observabilidad (FASE 16): éxito nutricional por análisis y razones
        // de fallo — sin datos de usuario.
        var nutritionOk = foods.Count(f => f.NutritionResult?.NutritionStatus == "available");
        var failures = foods
            .Where(f => f.NutritionResult is not null && f.NutritionResult.NutritionStatus != "available")
            .Select(f => $"{f.Name}:{f.NutritionResult!.NutritionStatus}")
            .ToList();
        // Telemetría nutricional por análisis (FASE 18): contadores por
        // estado. Sin datos de usuario ni imágenes.
        var statuses = foods
            .Where(f => f.NutritionResult is not null)
            .GroupBy(f => f.NutritionResult!.NutritionStatus)
            .ToDictionary(g => g.Key, g => g.Count());
        _logger.LogInformation(
            "nutrition_resumen analysis_id={AnalysisId} foods={Foods} nutricion_ok={Ok}/{Total} "
            + "nutrition_items_total={Total} nutrition_items_available={Available} "
            + "nutrition_items_unavailable={Unavailable} nutrition_mapping_missing={MappingMissing} "
            + "nutrition_mapping_ambiguous={MappingAmbiguous} nutrition_calculation_failed={CalcFailed} "
            + "fallos={Failures}",
            analysisId, foods.Count, nutritionOk, foods.Count, foods.Count,
            statuses.GetValueOrDefault("available"),
            statuses.GetValueOrDefault("portion_unavailable") + statuses.GetValueOrDefault("unavailable"),
            statuses.GetValueOrDefault("unavailable"),
            statuses.GetValueOrDefault("mapping_ambiguous"),
            statuses.GetValueOrDefault("calculation_failed"),
            failures.Count == 0 ? "ninguno" : string.Join(",", failures));

        await PersistAsync(analysisId, request.UserId, imageKey, result, foods, summary, ct);

        return new AnalyzeFoodImageResult(
            analysisId.ToString(), result.Status, result.ModelVersion, result.SegModelVersion,
            result.ClassifierVersion, result.InferenceTimeMs, foods,
            summary.Summary, summary.SummaryRange);
    }

    /// <summary>
    /// Persiste el análisis (snapshot nutricional + versiones + máscaras en
    /// object storage). Un fallo de persistencia se registra pero no rompe la
    /// respuesta del análisis (el resultado ya está calculado).
    /// </summary>
    private async Task PersistAsync(
        Guid analysisId,
        Guid? userId,
        string imageKey,
        FoodAiAnalyzeResult result,
        IReadOnlyList<DetectedFoodDto> foods,
        (NutritionValueDto? Summary, NutritionRangeDto? SummaryRange) summary,
        CancellationToken ct)
    {
        try
        {
            var analysis = new FoodAnalysis
            {
                AnalysisId = analysisId,
                Status = "completed",
                ImageKey = imageKey,
                UserId = userId,
                DetectorVersion = result.ModelVersion,
                SegmenterVersion = result.SegModelVersion,
                ClassifierVersion = result.ClassifierVersion,
                PortionMethod = foods.FirstOrDefault()?.Portion?.Method ?? "unknown",
                DepthModelVersion = null,
                SummaryCalories = summary.Summary?.Calories,
                SummaryProtein = summary.Summary?.Protein,
                SummaryCarbohydrates = summary.Summary?.Carbohydrates,
                SummaryFat = summary.Summary?.Fat,
                SummaryFiber = summary.Summary?.Fiber,
                SummarySugar = summary.Summary?.Sugar,
                SummarySodium = summary.Summary?.Sodium,
                Source = foods.FirstOrDefault(f => f.NutritionResult?.Source is not null)?.NutritionResult?.Source,
                SourceVersion = foods.FirstOrDefault(f => f.NutritionResult?.SourceVersion is not null)?.NutritionResult?.SourceVersion,
            };

            for (var index = 0; index < foods.Count; index++)
            {
                var food = foods[index];
                string? maskKey = null;
                if (food.Segmentation is not null && !string.IsNullOrWhiteSpace(food.Segmentation.Mask))
                {
                    try
                    {
                        var maskBytes = Convert.FromBase64String(food.Segmentation.Mask);
                        await using var maskStream = new MemoryStream(maskBytes);
                        maskKey = await _imageStorage.SaveMaskAsync(analysisId, index, maskStream, ct);
                    }
                    catch (Exception ex)
                    {
                        // La máscara no bloquea la persistencia del análisis.
                        _logger.LogWarning(ex, "No se pudo guardar la máscara del item {Index}", index);
                    }
                }

                analysis.Items.Add(new FoodAnalysisItem
                {
                    ItemIndex = index,
                    Name = food.Name,
                    DetectionConfidence = food.Confidence,
                    BboxX = food.BoundingBox.X,
                    BboxY = food.BoundingBox.Y,
                    BboxWidth = food.BoundingBox.Width,
                    BboxHeight = food.BoundingBox.Height,
                    MaskKey = maskKey,
                    MaskAreaPixels = food.Segmentation?.AreaPixels,
                    PortionSize = food.Portion?.PortionSize,
                    EstimatedGrams = food.Portion?.EstimatedGrams,
                    MinGrams = food.Portion?.MinGrams,
                    MaxGrams = food.Portion?.MaxGrams,
                    PortionConfidence = food.Portion?.Confidence,
                    PortionMethod = food.Portion?.Method,
                    NutritionStatus = food.NutritionResult?.NutritionStatus,
                    Calories = food.NutritionResult?.Nutrition?.Calories,
                    Protein = food.NutritionResult?.Nutrition?.Protein,
                    Carbohydrates = food.NutritionResult?.Nutrition?.Carbohydrates,
                    Fat = food.NutritionResult?.Nutrition?.Fat,
                    Fiber = food.NutritionResult?.Nutrition?.Fiber,
                    Sugar = food.NutritionResult?.Nutrition?.Sugar,
                    Sodium = food.NutritionResult?.Nutrition?.Sodium,
                    Source = food.NutritionResult?.Source,
                    SourceVersion = food.NutritionResult?.SourceVersion,
                });
            }

            await _analysisRepository.AddAsync(analysis, ct);
            _logger.LogInformation("Análisis persistido: AnalysisId={AnalysisId}, Items={ItemCount}", analysisId, foods.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la persistencia del análisis {AnalysisId}", analysisId);
        }
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