using CoppAddresd.Application.DTOs.FoodAi;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Calculadora nutricional: convierte valores por 100 g en valores para los
/// gramos estimados (nutrient × grams / 100), con rango derivado de
/// min/max gramos. Aritmética decimal; redondeo solo de display (2 decimales).
/// Separada de <see cref="INutritionProvider"/> (datos) y de los controllers.
/// </summary>
public interface INutritionCalculator
{
    FoodNutritionResult Calculate(
        FoodNutritionDto per100g,
        int? estimatedGrams,
        int? minGrams,
        int? maxGrams);
}