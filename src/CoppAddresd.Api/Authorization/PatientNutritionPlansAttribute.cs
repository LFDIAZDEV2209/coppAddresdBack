using System.Collections;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoppAddresd.Api.Authorization;

/// <summary>Valida referencias nutricionales del contenido semanal en una consulta, sin N+1.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PatientNutritionPlansAttribute(string parameter) : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public int Order => -900;
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.ActionArguments.TryGetValue(parameter, out var value);
        var values = value is IEnumerable items ? items.Cast<object>() : value is null ? [] : new[] { value };
        var ids = values.Select(x => x.GetType().GetProperty("NutritionPlanId")?.GetValue(x)).OfType<Guid>().ToArray();
        await context.HttpContext.RequestServices.GetRequiredService<IPatientScopeGuard>()
            .EnsureNutritionPlansAsync(ids, context.HttpContext.RequestAborted);
        await next();
    }
}
