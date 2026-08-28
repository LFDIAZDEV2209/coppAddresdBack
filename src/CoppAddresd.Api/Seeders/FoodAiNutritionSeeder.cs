using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seed del catálogo nutricional de Food AI (schema foodai).
///
/// Fuente: USDA FoodData Central (fdc.nal.usda.gov) — datos de dominio
/// público del gobierno de EE. UU. (sin restricción de licencia). Valores por
/// 100 g, tomados manualmente de las entradas FDC referenciadas en
/// docs/nutrition.md (fecha de consulta: 2026-08-27).
///
/// Idempotente por alias (los aliases son los nombres del modelo de visión).
/// Cada operación usa un scope propio con su propio DbContext, igual que
/// ClinicalMeasurementsSeeder.
/// </summary>
public sealed class FoodAiNutritionSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<FoodAiNutritionSeeder> logger) : IHostedService
{
    private const string Source = "USDA FoodData Central";
    private const string SourceVersion = "2026-08-27";

    private sealed record FoodSeed(
        string Alias,
        string Name,
        string DisplayName,
        string Category,
        decimal Calories,
        decimal Protein,
        decimal Carbohydrates,
        decimal Fat,
        decimal Fiber,
        decimal Sugar,
        decimal Sodium);

    // Valores por 100 g (unidad canónica). Alias = clase YOLO.
    // "sandwich" NO se incluye: sin entrada FDC fiable para un sándwich
    // genérico — no se inventan equivalencias (ver docs/nutrition.md).
    private static readonly IReadOnlyList<FoodSeed> Foods =
    [
        new("banana", "Banana, raw", "Banana", "fruit", 89m, 1.09m, 22.84m, 0.33m, 2.6m, 12.23m, 1m),
        new("apple", "Apple, raw, with skin", "Manzana", "fruit", 52m, 0.26m, 13.81m, 0.17m, 2.4m, 10.39m, 1m),
        new("orange", "Orange, raw", "Naranja", "fruit", 47m, 0.94m, 11.75m, 0.12m, 2.4m, 9.35m, 0m),
        new("broccoli", "Broccoli, raw", "Brócoli", "vegetable", 34m, 2.82m, 6.64m, 0.37m, 2.6m, 1.7m, 33m),
        new("carrot", "Carrot, raw", "Zanahoria", "vegetable", 41m, 0.93m, 9.58m, 0.24m, 2.8m, 4.74m, 69m),
        new("pizza", "Pizza, cheese, per 100 g", "Pizza", "prepared", 266m, 11.39m, 33.33m, 10.4m, 2.3m, 3.6m, 598m),
        new("hot dog", "Frankfurter, beef, per 100 g", "Hot dog", "prepared", 290m, 12.0m, 2.7m, 25.0m, 0m, 1.1m, 1050m),
        new("donut", "Doughnuts, cake-type, plain, per 100 g", "Dona", "confection", 452m, 4.9m, 51.3m, 25.4m, 1.5m, 24.8m, 445m),
        new("cake", "Cake, yellow, plain, without frosting, per 100 g", "Pastel", "confection", 361m, 5.3m, 53.2m, 14.6m, 0.7m, 27.9m, 392m),
    ];

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
        var seeded = 0;
        foreach (var seed in Foods)
        {
            var exists = await WithContext(
                db => db.FoodAliases.AnyAsync(a => a.Alias == seed.Alias, ct), ct);
            if (exists)
            {
                continue;
            }

            await WithContext(async db =>
            {
                var food = new Food
                {
                    Name = seed.Name,
                    DisplayName = seed.DisplayName,
                    Category = seed.Category,
                };
                food.NutritionEntries.Add(new FoodNutrition
                {
                    ServingGrams = 100m,
                    Calories = seed.Calories,
                    Protein = seed.Protein,
                    Carbohydrates = seed.Carbohydrates,
                    Fat = seed.Fat,
                    Fiber = seed.Fiber,
                    Sugar = seed.Sugar,
                    Sodium = seed.Sodium,
                    Source = Source,
                    SourceVersion = SourceVersion,
                });
                food.Aliases.Add(new FoodAlias
                {
                    Alias = seed.Alias,
                    Source = "yolo-coco-food-detector",
                });
                db.Foods.Add(food);
                await db.SaveChangesAsync(ct);
            }, ct);

            seeded++;
        }

        logger.LogInformation("Catálogo nutricional sembrado: {Seeded} alimentos nuevos", seeded);
    }

    private async Task<T> WithContext<T>(
        Func<AppDbContext, Task<T>> action,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    private Task WithContext(
        Func<AppDbContext, Task> action,
        CancellationToken ct)
        => WithContext(async db =>
        {
            await action(db);
            return true;
        }, ct);
}