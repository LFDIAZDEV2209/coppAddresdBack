using CoppAddresd.Application.DTOs.FoodAi;
using MediatR;

namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Consulta de nutrición por alimento (alias del modelo o nombre canónico).
/// Devuelve valores por porción canónica (100 g); null si el alimento no
/// existe o no tiene entrada nutricional.
/// </summary>
public record GetFoodNutritionQuery(string FoodKey) : IRequest<FoodNutritionDto?>;