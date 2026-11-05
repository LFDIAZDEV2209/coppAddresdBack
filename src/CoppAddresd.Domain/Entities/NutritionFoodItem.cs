namespace CoppAddresd.Domain.Entities;

/// <summary>Alimento del catálogo validado; nutrientes declarados por 100 gramos.</summary>
public sealed class NutritionFoodItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string FoodGroup { get; set; } = "";
    public double Calories100 { get; set; }
    public double Carbs100 { get; set; }
    public double Protein100 { get; set; }
    public double Fat100 { get; set; }
    public double Fiber100 { get; set; }
    public string? UnitSingular { get; set; }
    public string? UnitPlural { get; set; }
    public double? GramsPerUnit { get; set; }
    public double? UnitStep { get; set; }
}
