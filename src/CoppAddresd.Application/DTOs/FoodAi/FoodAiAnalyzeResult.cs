namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>Caja delimitadora en píxeles (formato x/y/width/height).</summary>
public record BoundingBoxDto(int X, int Y, int Width, int Height);

/// <summary>Alimento detectado por el Food AI Service.</summary>
public record DetectedFoodDto(string Name, double Confidence, BoundingBoxDto BoundingBox);

/// <summary>Resultado del análisis de una imagen (ingesta + detección).</summary>
public record FoodAiAnalyzeResult(
    string AnalysisId,
    string Status,
    string ModelVersion,
    int InferenceTimeMs,
    IReadOnlyList<DetectedFoodDto> Foods);