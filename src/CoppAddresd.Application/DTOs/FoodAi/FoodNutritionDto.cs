namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>
/// Valores nutricionales por porción canónica (ServingGrams, default 100 g).
/// Decimales, nunca float — precisión determinista.
/// </summary>
public record FoodNutritionDto(
    string FoodName,
    decimal ServingGrams,
    decimal Calories,
    decimal Protein,
    decimal Carbohydrates,
    decimal Fat,
    decimal Fiber,
    decimal Sugar,
    decimal Sodium,
    string Source,
    string SourceVersion,
    string? SourceId = null);