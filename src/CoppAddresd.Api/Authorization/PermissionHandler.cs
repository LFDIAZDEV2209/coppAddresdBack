using CoppAddresd.Api.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Api.Authorization;

/// <summary>
/// Autorización por claims (espejo del Auth Service): lee el claim
/// <c>permission</c> emitido en el access token. Cero consultas a la BD en el
/// hot path; los permisos con scope se evalúan aparte vía introspección.
/// </summary>
public class PermissionHandler(ILogger<PermissionHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaimTypes.Permission, requirement.PermissionCode))
        {
            logger.LogDebug("Permission claim {Permission} found, requirement succeeded", requirement.PermissionCode);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug("Permission claim {Permission} not found in token", requirement.PermissionCode);
        }

        return Task.CompletedTask;
    }
}
