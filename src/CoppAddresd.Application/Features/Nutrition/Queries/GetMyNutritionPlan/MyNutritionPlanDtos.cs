namespace CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;

/// <summary>
/// Plan de alimentación activo del paciente para la APP móvil (self-service
/// <c>GET /api/v1/me/nutrition-plan</c>). Contrato reducido: solo lectura del
/// plan asignado, sin datos del profesional ni de auditoría. Los días agrupan
/// sus comidas (<see cref="MyNutritionPlanDayDto.Meals"/>); <c>MealType</c>
/// viaja como nombre del enum de dominio (<c>Desayuno</c>/<c>Almuerzo</c>/
/// <c>Cena</c>/<c>Snack</c>, mismo contrato que el ERP).
/// </summary>
public sealed record MyNutritionPlanDto(
    Guid Id,
    string Name,
    string? TargetCondition,
    int DurationDays,
    int? DailyCalorieTarget,
    decimal? DailyProteinTarget,
    decimal? DailyCarbsTarget,
    decimal? DailyFatTarget,
    decimal? DailyFiberTarget,
    int DailyWaterMl,
    string? Allergens,
    string? MealTiming,
    string Status,
    List<MyNutritionPlanDayDto> Days
);

/// <summary>
/// Día del plan con sus comidas ordenadas por <c>SortOrder</c>.
/// <c>DailyWaterMl</c> es la meta de agua del día (convención: todas las filas
/// del día llevan el mismo valor; se expone el de la primera comida).
/// </summary>
public sealed record MyNutritionPlanDayDto(
    int DayNumber,
    int DailyWaterMl,
    List<MyNutritionPlanMealDto> Meals
);

/// <summary>
/// Comida del plan (una fila de <c>app.nutrition_plan_days</c>).
/// </summary>
public sealed record MyNutritionPlanMealDto(
    string MealType,
    string? Description,
    string? Foods,
    int? Calories,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    decimal? FiberG,
    int? WaterMl,
    string? Notes,
    int SortOrder
);
