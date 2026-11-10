using System.Security.Claims;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoppAddresd.Api.Authorization;

/// <summary>Runs before model binding, storage and SSE headers. ERP operator access retains its existing authorization.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireAiConsentAttribute(string scope) : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        { context.Result = new UnauthorizedResult(); return; }
        if (!user.HasClaim("aud", "erp") || user.HasClaim("aud", "app"))
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            { context.Result = new UnauthorizedResult(); return; }
            try
            {
                await context.HttpContext.RequestServices.GetRequiredService<ILegalConsentService>()
                    .EnsureUserAsync(userId, scope, context.HttpContext.RequestAborted);
            }
            catch (AiConsentException ex)
            {
                context.Result = new ObjectResult(new { code = ex.Code, scope = ex.Scope }) { StatusCode = ex.StatusCode };
                return;
            }
        }
        await next();
    }
}
