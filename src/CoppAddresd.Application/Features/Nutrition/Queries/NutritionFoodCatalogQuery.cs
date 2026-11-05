using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Queries;

public sealed record NutritionHouseholdEquivalent(string UnitSingular, string UnitPlural, double GramsPerUnit, double UnitStep);
public sealed record NutritionFoodDto(string Code, string Name, string Group, string Role,
    double Kcal100, double Carbs100, double Protein100, double Fat100, double Fiber100,
    NutritionHouseholdEquivalent? HouseholdEquivalent);
public sealed record NutritionFoodCatalogQuery(NutritionActor Actor) : IRequest<IReadOnlyList<NutritionFoodDto>>;

/// <summary>Catálogo común del editor: nutrientes declarados, sin recalcular kcal ni inventar equivalencias.</summary>
public sealed class NutritionFoodCatalogQueryHandler(INutritionPrescriptionRepository repository)
    : IRequestHandler<NutritionFoodCatalogQuery, IReadOnlyList<NutritionFoodDto>>
{
    public async Task<IReadOnlyList<NutritionFoodDto>> Handle(NutritionFoodCatalogQuery request, CancellationToken ct)
    {
        NutritionReadAccess.Ensure(request.Actor, ct);
        var foods = await repository.ListFoodsAsync(ct);
        return foods.OrderBy(x => x.Id,StringComparer.Ordinal).Select(food =>
        {
            ct.ThrowIfCancellationRequested();
            var group = food.FoodGroup switch { "C" => "Carbohidratos", "P" => "Proteínas",
                "F" => "Grasas", "V" => "Vegetales", _ => throw new InvalidOperationException("Grupo de alimento desconocido.") };
            return new NutritionFoodDto(food.Id,food.Name,group,food.FoodGroup,
                food.Calories100,food.Carbs100,food.Protein100,food.Fat100,food.Fiber100,
                food.GramsPerUnit is { } grams ? new(food.UnitSingular!,food.UnitPlural!,grams,food.UnitStep!.Value) : null);
        }).ToArray();
    }
}
