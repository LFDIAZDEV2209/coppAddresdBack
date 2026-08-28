using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Proveedor de nutrición sobre PostgreSQL (schema foodai): resuelve el alias
/// del modelo de visión → alimento canónico → entrada nutricional más
/// reciente (por ImportedAt). Sin datos → null (resultado controlado).
/// </summary>
public sealed class DatabaseNutritionProvider : INutritionProvider
{
    private readonly AppDbContext _db;

    public DatabaseNutritionProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FoodNutritionDto?> GetNutritionAsync(
        string foodKey,
        CancellationToken ct = default)
    {
        var normalizedKey = foodKey.Trim().ToLowerInvariant();
        if (normalizedKey.Length == 0)
        {
            return null;
        }

        // Alias primero (nombre del modelo de visión); fallback al nombre canónico.
        var nutrition = await (
            from alias in _db.FoodAliases
            where alias.Alias == normalizedKey
            join food in _db.Foods on alias.FoodId equals food.Id
            where food.IsActive
            join entry in _db.FoodNutritionEntries on food.Id equals entry.FoodId
            orderby entry.ImportedAt descending
            select new FoodNutritionDto(
                food.Name,
                entry.ServingGrams,
                entry.Calories,
                entry.Protein,
                entry.Carbohydrates,
                entry.Fat,
                entry.Fiber,
                entry.Sugar,
                entry.Sodium,
                entry.Source,
                entry.SourceVersion,
                entry.SourceId)
        ).FirstOrDefaultAsync(ct);

        if (nutrition is not null)
        {
            return nutrition;
        }

        return await (
            from food in _db.Foods
            where food.IsActive && food.Name.ToLower() == normalizedKey
            join entry in _db.FoodNutritionEntries on food.Id equals entry.FoodId
            orderby entry.ImportedAt descending
            select new FoodNutritionDto(
                food.Name,
                entry.ServingGrams,
                entry.Calories,
                entry.Protein,
                entry.Carbohydrates,
                entry.Fat,
                entry.Fiber,
                entry.Sugar,
                entry.Sodium,
                entry.Source,
                entry.SourceVersion)
        ).FirstOrDefaultAsync(ct);
    }
}