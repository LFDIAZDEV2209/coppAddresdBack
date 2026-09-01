namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>
/// Valores nutricionales calculados (decimal, 2 decimales de display).
/// </summary>
public record NutritionValueDto(
    decimal Calories,
    decimal Protein,
    decimal Carbohydrates,
    decimal Fat,
    decimal Fiber,
    decimal Sugar,
    decimal Sodium);

/// <summary>Rango nutricional derivado del rango de gramos de la porción.</summary>
public record NutritionRangeDto(NutritionValueDto Min, NutritionValueDto Max);

/// <summary>
/// Nutrición calculada para un alimento. status:
/// available | unavailable (sin entrada en la DB) | portion_unavailable (sin gramos).
/// </summary>
public record FoodNutritionResult(
    NutritionValueDto? Nutrition,
    NutritionRangeDto? NutritionRange,
    string NutritionStatus,
    string? Source = null,
    string? SourceVersion = null);