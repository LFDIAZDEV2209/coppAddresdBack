using System.Globalization;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed partial class ProgramRepository
{
    /// <summary>Carga únicamente el día prescrito; no trae los treinta días para una misión.</summary>
    private Task<NutritionPrescription?> ReadMissionPrescriptionAsync(Guid patientId, Guid id, int dayNumber, CancellationToken ct)
        => dbContext.NutritionPrescriptions.AsNoTracking()
            .Where(p => p.Id == id && p.PatientId == patientId && p.IsPublished && p.State == "aprobado")
            .Include(p => p.Meals.Where(m => m.DayNumber == dayNumber))
            .ThenInclude(m => m.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .ThenInclude(i => i.FoodItem)
            .AsSingleQuery().SingleOrDefaultAsync(ct);

    private static MealType PrescriptionMealType(string code) => code switch
    {
        "des" => MealType.Desayuno,
        "alm" => MealType.Almuerzo,
        "mer" => MealType.Snack,
        "cen" => MealType.Cena,
        _ => throw new InvalidOperationException("Tipo de comida prescrita inválido."),
    };

    /// <summary>Adapta la prescripción al DTO existente usando el motor Atwater canónico.</summary>
    private static IReadOnlyList<NutritionMealDto> PrescriptionMeals(NutritionPrescription plan)
    {
        var foods = plan.Meals.SelectMany(m => m.Items).Select(i => i.FoodItem!)
            .DistinctBy(f => f.Id).ToDictionary(f => f.Id);
        var engine = new NutritionEngine(foods);
        return plan.Meals.OrderBy(m => Array.IndexOf(NutritionEngine.MealOrder, m.MealType))
            .Select(m =>
            {
                var items = m.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToArray();
                var macros = engine.SumItems(items.Select(i => new NutritionItem(i.FoodItemId, i.Grams)));
                var text = string.Join(", ", items.Select(i =>
                    $"{i.FoodItem!.Name} x {i.Grams.ToString("0.##", CultureInfo.InvariantCulture)} g"));
                return new NutritionMealDto(PrescriptionMealType(m.MealType).ToString(), m.MenuName, text,
                    (int)macros.K, (decimal)macros.P, (decimal)macros.C, (decimal)macros.F, (decimal)macros.Fi,
                    m.WaterMl, m.Notes ?? plan.Instructions, Array.IndexOf(NutritionEngine.MealOrder, m.MealType));
            }).ToArray();
    }
}
