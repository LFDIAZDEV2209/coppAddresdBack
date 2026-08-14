using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Auth.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    // Retenido para un futuro feature flag de rollback a validación por BD
    // (REQ-CLAIMS-03: "May be retained behind a feature flag for rollback").
    // NO se consulta en el hot path: la autorización es 100% por claims.
    internal readonly IPermissionService? _permissionService;

    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(
        IPermissionService? permissionService,
        ILogger<PermissionHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Autorización basada en claims (REQ-CLAIMS-03): lee los códigos de
        // permiso emitidos en el access token (login/refresh). Cero queries a
        // la BD en el hot path; la revocación ERP se cubre con el security stamp.
        if (context.User.HasClaim(PermissionClaimTypes.Permission, requirement.PermissionCode))
        {
            _logger.LogDebug("Permission claim {Permission} found, requirement succeeded", requirement.PermissionCode);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("Permission claim {Permission} not found in token", requirement.PermissionCode);
        }

        return Task.CompletedTask;
    }
}
