namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>
/// Corrección del usuario sobre un análisis. Tipos: FOOD_WRONG,
/// PORTION_WRONG, DETECTION_WRONG, MISSING_FOOD, OTHER.
/// </summary>
public record FoodFeedbackRequest(
    int? ItemIndex,
    string Type,
    string? CorrectedFood = null,
    int? CorrectedGrams = null,
    string? Note = null);