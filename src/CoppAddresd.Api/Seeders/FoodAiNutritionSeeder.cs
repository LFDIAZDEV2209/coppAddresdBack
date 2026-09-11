using System.Text.Json;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seed del catálogo nutricional de Food AI (schema foodai) a partir del
/// archivo curado <c>Seeders/data/food_usda_curated.json</c>.
///
/// Fuente: USDA FoodData Central — datos de dominio público (CC0 1.0).
/// El JSON registra por alimento: fdc_id, nombre FDC, status del mapping
/// (DIRECT_MATCH/GOOD_EQUIVALENCE/REVIEW_REQUIRED/NO_RELIABLE_MATCH),
/// confianza del mapping y valores por 100 g. Alimentos sin nutrition (null)
/// NO se insertan (identificación visual ≠ disponibilidad nutricional).
///
/// Idempotente por alias (nombres del modelo de visión). Reconstruible desde
/// entorno limpio. Actualizar valores = editar el JSON y re-ejecutar (el
/// alias existente no se duplica; para refrescar un valor se usa el campo
/// source_version del JSON en futuras versiones).
/// </summary>
public sealed class FoodAiNutritionSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<FoodAiNutritionSeeder> logger
) : IHostedService
{
    private const string CuratedDataPath = "Seeders/data/food_usda_curated.json";

    private sealed record CuratedFood(
        string Canonical,
        string DisplayName,
        string Category,
        string Alias,
        string? FdcId,
        string? FdcName,
        string MappingStatus,
        decimal MappingConfidence,
        CuratedNutrition? Nutrition
    );

    private sealed record CuratedNutrition(
        decimal Calories,
        decimal Protein,
        decimal Carbohydrates,
        decimal Fat,
        decimal Fiber,
        decimal Sugar,
        decimal Sodium
    );

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed del catálogo nutricional cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el seed del catálogo nutricional");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var foods = LoadCurated();
        if (foods.Count == 0)
        {
            logger.LogWarning("Catálogo curado vacío o no encontrado: {Path}", CuratedDataPath);
            return;
        }

        var seeded = 0;
        foreach (var seed in foods)
        {
            if (seed.Nutrition is null)
            {
                continue; // sin equivalencia confiable → sin fila nutricional
            }

            var exists = await WithContext(
                db => db.FoodAliases.AnyAsync(a => a.Alias == seed.Alias, ct),
                ct
            );
            if (exists)
            {
                continue;
            }

            await WithContext(
                async db =>
                {
                    // Varias entradas curadas pueden compartir el mismo alimento
                    // FDC (p. ej. "chicken" y "grilled chicken"): se reutiliza la
                    // fila existente y solo se agrega el alias (Name es único).
                    var foodName = seed.FdcName ?? seed.Canonical;
                    var food = await db
                        .Foods.Include(f => f.Aliases)
                        .FirstOrDefaultAsync(f => f.Name == foodName, ct);
                    if (food is null)
                    {
                        food = new Food
                        {
                            Name = foodName,
                            DisplayName = seed.DisplayName,
                            Category = seed.Category,
                            MappingStatus = seed.MappingStatus,
                            MappingConfidence = seed.MappingConfidence,
                        };
                        food.NutritionEntries.Add(
                            new FoodNutrition
                            {
                                ServingGrams = 100m,
                                Calories = seed.Nutrition.Calories,
                                Protein = seed.Nutrition.Protein,
                                Carbohydrates = seed.Nutrition.Carbohydrates,
                                Fat = seed.Nutrition.Fat,
                                Fiber = seed.Nutrition.Fiber,
                                Sugar = seed.Nutrition.Sugar,
                                Sodium = seed.Nutrition.Sodium,
                                Source = "USDA FoodData Central",
                                SourceVersion = "2026-08-28",
                                SourceId = seed.FdcId,
                            }
                        );
                        db.Foods.Add(food);
                    }

                    food.Aliases.Add(
                        new FoodAlias { Alias = seed.Alias, Source = "food-catalog-clip" }
                    );
                    await db.SaveChangesAsync(ct);
                },
                ct
            );

            seeded++;
        }

        logger.LogInformation(
            "Catálogo nutricional sembrado: {Seeded} nuevos de {Total} curados",
            seeded,
            foods.Count
        );
    }

    private static List<CuratedFood> LoadCurated()
    {
        var path = Path.Combine(AppContext.BaseDirectory, CuratedDataPath);
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<CuratedFood>();
        foreach (var element in doc.RootElement.GetProperty("foods").EnumerateArray())
        {
            var nutritionElement = element.GetProperty("nutrition");
            CuratedNutrition? nutrition = null;
            if (nutritionElement.ValueKind == JsonValueKind.Object)
            {
                nutrition = new CuratedNutrition(
                    Calories: nutritionElement.GetProperty("calories").GetDecimal(),
                    Protein: nutritionElement.GetProperty("protein").GetDecimal(),
                    Carbohydrates: nutritionElement.GetProperty("carbohydrates").GetDecimal(),
                    Fat: nutritionElement.GetProperty("fat").GetDecimal(),
                    Fiber: nutritionElement.GetProperty("fiber").GetDecimal(),
                    Sugar: nutritionElement.GetProperty("sugar").GetDecimal(),
                    Sodium: nutritionElement.GetProperty("sodium").GetDecimal()
                );
            }

            list.Add(
                new CuratedFood(
                    Canonical: element.GetProperty("canonical").GetString()!,
                    DisplayName: element.GetProperty("display_name").GetString()!,
                    Category: element.GetProperty("category").GetString()!,
                    Alias: element.GetProperty("alias").GetString()!,
                    FdcId: element.GetProperty("fdc_id").ValueKind == JsonValueKind.String
                        ? element.GetProperty("fdc_id").GetString()
                        : null,
                    FdcName: element.GetProperty("fdc_name").ValueKind == JsonValueKind.String
                        ? element.GetProperty("fdc_name").GetString()
                        : null,
                    MappingStatus: element.GetProperty("mapping_status").GetString()!,
                    MappingConfidence: element.GetProperty("mapping_confidence").GetDecimal(),
                    Nutrition: nutrition
                )
            );
        }

        return list;
    }

    private async Task<T> WithContext<T>(Func<AppDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    private Task WithContext(Func<AppDbContext, Task> action, CancellationToken ct) =>
        WithContext(
            async db =>
            {
                await action(db);
                return true;
            },
            ct
        );
}
