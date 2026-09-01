using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.FoodAi;

public class GetFoodNutritionQueryHandler
    : IRequestHandler<GetFoodNutritionQuery, FoodNutritionDto?>
{
    private readonly INutritionProvider _nutritionProvider;

    public GetFoodNutritionQueryHandler(INutritionProvider nutritionProvider)
    {
        _nutritionProvider = nutritionProvider;
    }

    public Task<FoodNutritionDto?> Handle(
        GetFoodNutritionQuery request,
        CancellationToken ct)
        => _nutritionProvider.GetNutritionAsync(request.FoodKey, ct);
}