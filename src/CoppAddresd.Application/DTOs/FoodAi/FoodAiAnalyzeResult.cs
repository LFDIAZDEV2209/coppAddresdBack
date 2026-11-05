namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>Caja delimitadora en píxeles (formato x/y/width/height).</summary>
public record BoundingBoxDto(int X, int Y, int Width, int Height);

/// <summary>Máscara de segmentación: PNG en escala de grises (base64) recortado al bbox.</summary>
public record SegmentationDto(string Mask, int AreaPixels);

/// <summary>Macros aproximados por 100 g del LLM, usados solo sin entrada USDA.</summary>
public record EstimatedNutrientsDto(decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG, decimal FiberG);

/// <summary>
/// Estimación de porción (basic_reference). estimatedGrams != measuredGrams:
/// aproximación con referencia documentada, nunca peso medido.
/// </summary>
public record PortionDto(
    string PortionSize,
    int? EstimatedGrams,
    int? MinGrams,
    int? MaxGrams,
    double Confidence,
    string Method);

/// <summary>Alimento detectado por el Food AI Service.</summary>
public record DetectedFoodDto(
    string Name,
    double Confidence,
    BoundingBoxDto BoundingBox,
    SegmentationDto? Segmentation = null,
    PortionDto? Portion = null,
    FoodNutritionResult? NutritionResult = null,
    string? NutritionQuery = null,
    string? NameEn = null,
    EstimatedNutrientsDto? EstimatedNutrients = null);

/// <summary>Resultado del análisis de una imagen (ingesta + detección + segmentación + clasificación).</summary>
public record FoodAiAnalyzeResult(
    string AnalysisId,
    string Status,
    string ModelVersion,
    string SegModelVersion,
    string ClassifierVersion,
    int InferenceTimeMs,
    IReadOnlyList<DetectedFoodDto> Foods,
    string? Provider = null);
