namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Alimento del catálogo nutricional (schema foodai). El nombre canónico
/// proviene de la fuente nutricional (USDA FDC); los nombres del modelo de
/// visión se mapean vía <see cref="FoodAlias"/>.
/// </summary>
public sealed class Food
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Category { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<FoodNutrition> NutritionEntries { get; set; } = [];
    public ICollection<FoodAlias> Aliases { get; set; } = [];
}