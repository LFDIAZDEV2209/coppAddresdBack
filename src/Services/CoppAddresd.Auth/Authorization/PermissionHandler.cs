using System.Security.Claims;
using CoppAddresd.Auth.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Auth.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(
        IPermissionService permissionService,
        ILogger<PermissionHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogDebug("Permission check failed: no valid user ID in claims");
            return;
        }

        var hasPermission = await _permissionService.UserHasPermissionAsync(userId, requirement.PermissionCode);

        if (hasPermission)
        {
            _logger.LogDebug("User {UserId} has permission {Permission}", userId, requirement.PermissionCode);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("User {UserId} does not have permission {Permission}", userId, requirement.PermissionCode);
        }
    }
}
