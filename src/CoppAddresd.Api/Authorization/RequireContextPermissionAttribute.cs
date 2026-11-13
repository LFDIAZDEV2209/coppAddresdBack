using CoppAddresd.Api.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoppAddresd.Api.Authorization;

/// <summary>Comprueba el permiso efectivo global o de clínica/organización sin exceptuar alcance.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireContextPermissionAttribute(string permissionCode) : Attribute, IAsyncAuthorizationFilter
{
    public string PermissionCode { get; } = permissionCode;
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        { context.Result = new ChallengeResult(); return; }
        var actor = context.HttpContext.RequestServices.GetRequiredService<ICurrentContext>();
        if (!await actor.HasPermissionAsync(PermissionCode, context.HttpContext.RequestAborted))
            context.Result = new ForbidResult();
    }
}
