namespace CoppAddresd.Domain.Entities;

/// <summary>Porción prescrita con referencia obligatoria al catálogo validado.</summary>
public sealed class NutritionPrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealId { get; set; }
    public string FoodItemId { get; set; } = "";
    public double Grams { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public NutritionPrescriptionMeal? Meal { get; set; }
    public NutritionFoodItem? FoodItem { get; set; }
}
