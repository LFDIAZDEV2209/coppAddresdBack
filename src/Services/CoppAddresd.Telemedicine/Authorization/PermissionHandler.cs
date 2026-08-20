using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Telemedicine.Authorization;

/// <summary>
/// Autorización por claims (espejo del Auth Service y del backend): lee el
/// claim <c>permission</c> emitido en el access token. Cero consultas a la BD
/// en el hot path.
/// </summary>
public class PermissionHandler(ILogger<PermissionHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private const string PermissionClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionClaimType, requirement.PermissionCode))
        {
            logger.LogDebug("Permiso {Permission} concedido", requirement.PermissionCode);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogDebug("Permiso {Permission} no presente en el token", requirement.PermissionCode);
        }

        return Task.CompletedTask;
    }
}
