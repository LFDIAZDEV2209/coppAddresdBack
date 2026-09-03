using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Mapa canónico mealType ↔ mealCode (D4, SPEC nutrition-intake-adherence):
/// Desayuno→des, Almuerzo→alm, Snack(Merienda)→mer, Cena→cen. <c>agua</c> es
/// hidratación y NUNCA entra en este mapa — no gatea (el gate cuenta solo
/// códigos de comida). Espejo backend de <c>src/utils/mealTypeToCode.ts</c>
/// del móvil; usado por el gate 6c de <c>CompleteTaskCoreAsync</c>.
/// </summary>
public static class NutritionMealCodeMapping
{
    /// <summary>
    /// Convierte un <see cref="MealType"/> del plan al <see cref="MealCode"/>
    /// del log. Devuelve <c>null</c> para tipos desconocidos — nunca un
    /// fallthrough silencioso: una comida sin código no puede exigir evidencia.
    /// </summary>
    public static MealCode? For(MealType mealType) => mealType switch
    {
        MealType.Desayuno => MealCode.des,
        MealType.Almuerzo => MealCode.alm,
        MealType.Snack => MealCode.mer,
        MealType.Cena => MealCode.cen,
        _ => null,
    };
}