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

    /// <summary>
    /// Estado del mapping nutricional (DIRECT_MATCH | GOOD_EQUIVALENCE |
    /// AMBIGUOUS | REVIEW_REQUIRED | NO_RELIABLE_MATCH). Independiente de la
    /// confianza de detección/clasificación.
    /// </summary>
    public string? MappingStatus { get; set; }

    /// <summary>Confianza del mapping visual → nutrición (0..1), separada de la clasificación.</summary>
    public decimal? MappingConfidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<FoodNutrition> NutritionEntries { get; set; } = [];
    public ICollection<FoodAlias> Aliases { get; set; } = [];
}