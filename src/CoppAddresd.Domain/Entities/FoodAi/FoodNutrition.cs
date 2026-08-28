namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Valores nutricionales de un alimento, expresados SIEMPRE por
/// <see cref="ServingGrams"/> gramos (unidad canónica 100 g).
/// Los valores provienen de la fuente referenciada (<see cref="Source"/>);
/// nunca los genera la IA.
/// </summary>
public sealed class FoodNutrition
{
    public Guid Id { get; set; }
    public Guid FoodId { get; set; }
    public decimal ServingGrams { get; set; } = 100m;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }
    public string Source { get; set; } = default!;
    public string SourceVersion { get; set; } = default!;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public Food Food { get; set; } = default!;
}