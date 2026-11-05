using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.Wellness;

public static class NutritionPlanReviewRules
{
    public static bool IsPublished(NutritionPlan plan) => plan.ApprovedAt is not null
        && plan.ApprovedBy is not null && plan.ReviewStatus == "approved" && plan.Status == NutritionPlanStatus.Active;

    public static void ValidateForApproval(NutritionPlan plan)
    {
        if (plan.IsTemplate || plan.PatientId is null || plan.ReviewStatus != "pending_review"
            || plan.Status == NutritionPlanStatus.Archived
            || plan.GenerationStatus is "queued" or "generating" or "failed")
            throw new UnprocessableEntityException("Solo se puede aprobar un borrador personalizado listo para revisión.");
        ValidateMenu(plan);
    }

    public static void ValidateMenu(NutritionPlan plan)
    {
        if (plan.DurationDays != 30 || string.IsNullOrWhiteSpace(plan.Name) || plan.Name.Length > 200
            || plan.Days.Count == 0 || plan.Days.Any(d => d.DayNumber is < 1 or > 30
                || string.IsNullOrWhiteSpace(d.Description) && string.IsNullOrWhiteSpace(d.Foods)))
            throw new UnprocessableEntityException("El ciclo debe durar 30 días y contener un menú válido.");
        var days = plan.Days.GroupBy(d => d.DayNumber).OrderBy(g => g.Key).ToList();
        if (days.Count is not (7 or 30)
            || days.Select(g => g.Key).Where((day, index) => day != index + 1).Any()
            || days.Any(g => new[] { MealType.Desayuno, MealType.Almuerzo, MealType.Snack, MealType.Cena }
                .Any(type => !g.Any(d => d.MealType == type))))
            throw new UnprocessableEntityException("El menú requiere 7 días semanales o 30 días completos, cada uno con desayuno, almuerzo, merienda y cena.");
    }

    public static int? MenuDay(NutritionPlan plan, DateTime startDate, DateOnly localDate)
    {
        if (!IsPublished(plan)) return null;
        var elapsed = localDate.DayNumber - DateOnly.FromDateTime(startDate).DayNumber;
        if (elapsed is < 0 or >= 30) return null;
        var count = plan.Days.Select(d => d.DayNumber).Distinct().Count();
        return count > 0 ? elapsed % count + 1 : null;
    }
}
