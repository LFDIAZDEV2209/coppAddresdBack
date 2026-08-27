namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Códigos de comida/hidratación que usa la app móvil (NutritionPage): los
/// nombres del enum son exactamente los valores que el cliente envía en el
/// contrato de la API y los códigos de las plantillas de hábito sembradas
/// (SPEC §18, B: <c>app.habit_templates.code</c> = <c>des</c>/<c>alm</c>/
/// <c>mer</c>/<c>cen</c>/<c>agua</c>). <c>des</c>/<c>alm</c>/<c>mer</c>/
/// <c>cen</c> son comidas (categoría <c>alimentacion</c>); <c>agua</c> es
/// hidratación (categoría <c>agua</c>).
/// </summary>
public enum MealCode
{
    /// <summary>Desayuno (comida).</summary>
    des = 1,

    /// <summary>Almuerzo (comida).</summary>
    alm = 2,

    /// <summary>Merienda (comida).</summary>
    mer = 3,

    /// <summary>Cena (comida).</summary>
    cen = 4,

    /// <summary>Hidratación (agua).</summary>
    agua = 5,
}