namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Ciclo de vida de un plan de alimentación.
/// Draft = borrador, Active = asignado y vigente, Completed = finalizado, Archived = inactivo.
/// </summary>
public enum NutritionPlanStatus
{
    Draft = 1,
    Active = 2,
    Completed = 3,
    Archived = 4,
}
