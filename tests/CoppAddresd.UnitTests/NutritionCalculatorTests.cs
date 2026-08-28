using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests de la calculadora nutricional: fórmula por gramos, precisión
/// decimal, rangos, casos nulos y suma de totales.
/// </summary>
public class NutritionCalculatorTests
{
    private static readonly FoodNutritionDto Banana100g = new(
        "Banana, raw", 100m, 89m, 1.09m, 22.84m, 0.33m, 2.6m, 12.23m, 1m,
        "USDA FoodData Central", "2026-08-27");

    private static readonly FoodNutritionDto Pizza100g = new(
        "Pizza, cheese, per 100 g", 100m, 266m, 11.39m, 33.33m, 10.4m, 2.3m, 3.6m, 598m,
        "USDA FoodData Central", "2026-08-27");

    private static readonly INutritionCalculator Calculator = new NutritionCalculator();

    [Fact]
    public void Calculate_100g_escala_1_a_1()
    {
        var result = Calculator.Calculate(Banana100g, 100, 80, 120);

        Assert.Equal("available", result.NutritionStatus);
        Assert.Equal(89m, result.Nutrition!.Calories);
        Assert.Equal(22.84m, result.Nutrition.Carbohydrates);
        Assert.Equal(1.09m, result.Nutrition.Protein);
    }

    [Fact]
    public void Calculate_50g_escala_medio()
    {
        var result = Calculator.Calculate(Banana100g, 50, 40, 60);

        Assert.Equal(44.5m, result.Nutrition!.Calories);   // 89 × 0.5
        Assert.Equal(11.42m, result.Nutrition.Carbohydrates); // 22.84 × 0.5
    }

    [Fact]
    public void Calculate_200g_escala_doble()
    {
        var result = Calculator.Calculate(Banana100g, 200, 160, 240);

        Assert.Equal(178m, result.Nutrition!.Calories);
        Assert.Equal(45.68m, result.Nutrition.Carbohydrates);
    }

    [Fact]
    public void Calculate_pizza_128g_valores_reales_de_la_db()
    {
        // 266 kcal × 128 / 100 = 340.48 (pizza de la Nutrition DB real)
        var result = Calculator.Calculate(Pizza100g, 128, 118, 160);

        Assert.Equal(340.48m, result.Nutrition!.Calories);
        Assert.Equal(42.66m, result.Nutrition.Carbohydrates);
        Assert.Equal(14.58m, result.Nutrition.Protein);
        Assert.Equal(13.31m, result.Nutrition.Fat);
    }

    [Fact]
    public void Calculate_redondeo_decimal_a_2()
    {
        var result = Calculator.Calculate(Banana100g, 118, 94, 142);

        Assert.Equal(105.02m, result.Nutrition!.Calories); // 89 × 1.18 = 105.02
        Assert.Equal(0.39m, result.Nutrition.Fat);         // 0.33 × 1.18 = 0.3894 → 0.39
    }

    [Fact]
    public void Calculate_rango_min_max()
    {
        var result = Calculator.Calculate(Banana100g, 118, 94, 142);

        Assert.NotNull(result.NutritionRange);
        Assert.Equal(83.66m, result.NutritionRange!.Min.Calories); // 89 × 0.94
        Assert.Equal(126.38m, result.NutritionRange.Max.Calories); // 89 × 1.42
        Assert.True(result.NutritionRange.Min.Calories <= result.Nutrition.Calories);
        Assert.True(result.Nutrition.Calories <= result.NutritionRange.Max.Calories);
    }

    [Fact]
    public void Calculate_sin_gramos_devuelve_portion_unavailable()
    {
        var result = Calculator.Calculate(Banana100g, null, null, null);

        Assert.Equal("portion_unavailable", result.NutritionStatus);
        Assert.Null(result.Nutrition);
        Assert.Null(result.NutritionRange);
    }

    [Fact]
    public void Calculate_gramos_cero_devuelve_portion_unavailable()
    {
        var result = Calculator.Calculate(Banana100g, 0, null, null);

        Assert.Equal("portion_unavailable", result.NutritionStatus);
    }

    [Fact]
    public void Calculate_source_se_conserva()
    {
        var result = Calculator.Calculate(Banana100g, 118, 94, 142);

        Assert.Equal("USDA FoodData Central", result.Source);
        Assert.Equal("2026-08-27", result.SourceVersion);
    }

    [Fact]
    public void Calculate_aritmetica_decimal_no_float()
    {
        // 0.1 + 0.2 en float daría 0.30000000000000004; decimal da 0.3 exacto.
        var dto = Banana100g with { Calories = 0.1m, Carbohydrates = 0.2m };
        var result = Calculator.Calculate(dto, 300, 250, 350);

        Assert.Equal(0.3m, result.Nutrition!.Calories);      // 0.1 × 3
        Assert.Equal(0.6m, result.Nutrition.Carbohydrates);  // 0.2 × 3
    }

    [Fact]
    public void Sumatoria_de_totales_no_doble_contabilizacion()
    {
        // Simula el handler: un alimento con nutrición disponible y otro sin ella.
        var banana = new FoodNutritionResult(
            Calculator.Calculate(Banana100g, 118, 94, 142).Nutrition,
            Calculator.Calculate(Banana100g, 118, 94, 142).NutritionRange,
            "available", "USDA FoodData Central", "2026-08-27");
        var sandwich = new FoodNutritionResult(null, null, "unavailable");

        // Suma solo los disponibles (un solo alimento aquí).
        var available = new[] { banana, sandwich }
            .Where(r => r.NutritionStatus == "available" && r.Nutrition is not null)
            .ToList();
        Assert.Single(available);
        Assert.Equal(105.02m, available[0].Nutrition!.Calories);
    }
}