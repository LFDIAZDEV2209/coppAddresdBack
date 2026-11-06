using System.Text.Json;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Recupera nombres de análisis legacy desde el mismo catálogo empacado que usa
/// FoodAiNutritionSeeder. Solo identidades exactas y no ambiguas: nunca usa una
/// equivalencia USDA para cambiar la identidad de lo que detectó el análisis.
/// </summary>
public sealed class CuratedFoodNameCatalog : IFoodNameCatalog
{
    private readonly IReadOnlyDictionary<string, string> names;

    public CuratedFoodNameCatalog(ILogger<CuratedFoodNameCatalog> logger)
        : this(Path.Combine(AppContext.BaseDirectory, "Seeders", "data", "food_usda_curated.json"), logger) { }

    public CuratedFoodNameCatalog(string path, ILogger<CuratedFoodNameCatalog> logger)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var candidates = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var food in document.RootElement.GetProperty("foods").EnumerateArray())
            {
                var canonical = food.GetProperty("canonical").GetString();
                if (string.IsNullOrWhiteSpace(canonical)) continue;
                var english = canonical.Replace('_', ' ');
                foreach (var source in new[] { canonical, food.GetProperty("display_name").GetString() })
                {
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    if (!candidates.TryGetValue(source.Trim(), out var labels))
                        candidates[source.Trim()] = labels = new(StringComparer.Ordinal);
                    labels.Add(english);
                }
            }
            names = candidates.Where(pair => pair.Value.Count == 1)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Single(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            // Un catálogo no disponible deja los nombres a la traducción explícita;
            // nunca se inventa un nombre ni se devuelve otro idioma como éxito.
            logger.LogWarning("Catálogo de nombres Food AI no disponible: {ExceptionType}", error.GetType().Name);
            names = new Dictionary<string, string>();
        }
    }

    public Task<IReadOnlyDictionary<string, string>> GetEnglishNamesAsync(
        IReadOnlyCollection<string> sourceNames, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sourceNames)
            if (names.TryGetValue(source.Trim(), out var english)) result[source] = english;
        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }
}
