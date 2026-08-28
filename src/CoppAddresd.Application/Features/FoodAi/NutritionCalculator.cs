using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Calculadora nutricional determinista: nutrient × grams / 100 con
/// aritmética decimal (sin float). Redondeo solo de display (2 decimales).
/// Sin gramos → portion_unavailable; sin datos → unavailable (no inventa).
/// </summary>
public sealed class NutritionCalculator : INutritionCalculator
{
    private const decimal Hundred = 100m;

    public FoodNutritionResult Calculate(
        FoodNutritionDto per100g,
        int? estimatedGrams,
        int? minGrams,
        int? maxGrams)
    {
        if (estimatedGrams is null || estimatedGrams <= 0)
        {
            return new FoodNutritionResult(
                Nutrition: null,
                NutritionRange: null,
                NutritionStatus: "portion_unavailable");
        }

        var nutrition = Scale(per100g, estimatedGrams.Value);

        if (minGrams is not null && maxGrams is not null && minGrams > 0 && maxGrams >= minGrams)
        {
            var range = new NutritionRangeDto(
                Min: Scale(per100g, minGrams.Value),
                Max: Scale(per100g, maxGrams.Value));
            return new FoodNutritionResult(
                Nutrition: nutrition,
                NutritionRange: range,
                NutritionStatus: "available",
                Source: per100g.Source,
                SourceVersion: per100g.SourceVersion);
        }

        return new FoodNutritionResult(
            Nutrition: nutrition,
            NutritionRange: null,
            NutritionStatus: "available",
            Source: per100g.Source,
            SourceVersion: per100g.SourceVersion);
    }

    /// <summary>Valores por gramos: nutrient_per_100g × grams / 100, 2 decimales.</summary>
    private static NutritionValueDto Scale(FoodNutritionDto per100g, int grams)
    {
        decimal Factor(decimal value) => Math.Round(value * grams / Hundred, 2, MidpointRounding.AwayFromZero);

        return new NutritionValueDto(
            Calories: Factor(per100g.Calories),
            Protein: Factor(per100g.Protein),
            Carbohydrates: Factor(per100g.Carbohydrates),
            Fat: Factor(per100g.Fat),
            Fiber: Factor(per100g.Fiber),
            Sugar: Factor(per100g.Sugar),
            Sodium: Factor(per100g.Sodium));
    }
}