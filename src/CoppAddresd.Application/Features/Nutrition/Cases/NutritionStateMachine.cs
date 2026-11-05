using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

/// <summary>Estados persistidos y vigencia por calendario local; por_renovar es virtual.</summary>
public static class NutritionStateMachine
{
    public static void Ensure(NutritionOperation operation, string state)
    {
        var allowed = operation switch
        {
            NutritionOperation.CreateVersion => state == "aprobado",
            NutritionOperation.Save or NutritionOperation.ApplyDay or NutritionOperation.Regenerate or NutritionOperation.Submit
                => state is "borrador_ia" or "devuelto",
            NutritionOperation.Return or NutritionOperation.Approve => state == "en_aprobacion",
            _ => false
        };
        if (!allowed) throw new BusinessRuleViolationException("Transición nutricional inválida; la versión aprobada es inmutable.");
    }
    public static DateOnly Today(DateTimeOffset utcNow, string timezone)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timezone)).DateTime); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new UnprocessableEntityException("La zona horaria del paciente no es válida."); }
    }
    public static (DateOnly Start, DateOnly End) Dates(DateOnly today, DateOnly? requested, NutritionPrescription? previous)
    {
        if (previous is not null)
        {
            if (previous.StartDate is not { } start || previous.EndDate != start.AddDays(29) || previous.EndDate < today)
                throw new BusinessRuleViolationException("El ciclo terminó o es inválido; genera un nuevo ciclo de 30 días.");
            if (requested.HasValue && requested != start)
                throw new BusinessRuleViolationException("Una nueva versión conserva la vigencia del ciclo.");
            return (start, start.AddDays(29));
        }
        var begin = requested ?? today;
        if (begin < today) throw new UnprocessableEntityException("No se permite publicar con fecha anterior a hoy local.");
        return (begin, begin.AddDays(29));
    }
    public static string DisplayState(NutritionPrescription plan, DateOnly today)
        => plan.State == "aprobado" && plan.IsPublished && plan.EndDate <= today.AddDays(5) ? "por_renovar" : plan.State;
}
