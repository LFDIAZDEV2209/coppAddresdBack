using CoppAddresd.Application.DTOs.FoodAi;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Proveedor de información nutricional. Recibe un identificador de alimento
/// (nombre canónico o alias del modelo de visión) y devuelve la nutrición por
/// porción canónica (100 g). NUNCA calcula gramos finales ni recibe valores
/// de la IA de visión: separación estricta IA ↔ nutrición.
/// </summary>
public interface INutritionProvider
{
    /// <summary>
    /// Devuelve la nutrición por 100 g del alimento identificado por
    /// <paramref name="foodKey"/> (alias o nombre canónico), o null si no existe.
    /// </summary>
    Task<FoodNutritionDto?> GetNutritionAsync(string foodKey, CancellationToken ct = default);
}