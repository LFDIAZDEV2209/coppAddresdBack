using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoppAddresd.Api.Authorization;

/// <summary>Ejecuta el guard antes de la acción y de cualquier lectura, escritura o firma.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class PatientScopeAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public string Parameter { get; }
    public string Permission { get; }
    public PatientResource? Resource { get; }
    public bool SkipForTemplate { get; set; }
    public bool Optional { get; set; }
    public bool AllowUnowned { get; set; }
    public bool NutritionGeneration { get; set; }
    public int Order => -1000;

    public PatientScopeAttribute(string parameter, string permission)
        => (Parameter, Permission) = (parameter, permission);
    public PatientScopeAttribute(PatientResource resource, string parameter, string permission)
        => (Resource, Parameter, Permission) = (resource, parameter, permission);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = Parameter.Split('.');
        context.ActionArguments.TryGetValue(path[0], out var value);
        var permission = NutritionGeneration && value?.GetType().GetProperty("Type")?.GetValue(value) is "nutrition"
            ? "Nutrition.Manage" : Permission;
        if (SkipForTemplate && value?.GetType().GetProperty("TargetType")?.GetValue(value) is string type
            && string.Equals(type.Trim(), "Template", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }
        foreach (var segment in path.Skip(1)) value = value?.GetType().GetProperty(segment)?.GetValue(value);
        if (value is null && Optional) { await next(); return; }
        if (value is not Guid id) throw new NotFoundException("Recurso no encontrado.");
        var guard = context.HttpContext.RequestServices.GetRequiredService<IPatientScopeGuard>();
        if (Resource is { } resource)
        {
            if (AllowUnowned)
            {
                await guard.GetFilterAsync(permission, context.HttpContext.RequestAborted);
                var owner = await context.HttpContext.RequestServices.GetRequiredService<IPatientScopeStore>()
                    .OwnerAsync(resource, id, context.HttpContext.RequestAborted);
                if (owner is { } patient) await guard.EnsureAsync(patient, permission, context.HttpContext.RequestAborted);
            }
            else await guard.EnsureResourceAsync(resource, id, permission, context.HttpContext.RequestAborted);
        }
        else await guard.EnsureAsync(id, permission, context.HttpContext.RequestAborted);
        await next();
    }
}
