using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Gestión de asignaciones de roles y permisos con scope (excepciones por
/// contexto del cliente). Complementa el módulo de usuarios: aquí un usuario
/// recibe roles/permisos DENTRO de una clínica/organización concreta. Exige
/// los permisos de asignación (Roles.Assign / Permissions.Assign) + aud ERP.
/// </summary>
[ApiController]
[Route("api/auth/users/{userId:guid}/scoped")]
[Authorize]
[RequireErpAudience]
public class ScopedAssignmentsController(
    IScopedPermissionService scopedPermissions,
    ITokenInvalidationService tokenInvalidation) : ControllerBase
{
    [HttpPost("roles")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> AssignRole(
        Guid userId,
        [FromBody] ScopedRoleRequest request,
        CancellationToken ct)
    {
        var grantedBy = GetCallerId();
        var (success, error) = await scopedPermissions.AssignRoleAsync(
            userId, request.RoleId, request.ScopeType, request.ScopeId, grantedBy, ct);

        if (!success)
            return BadRequest(new { message = error });

        await tokenInvalidation.InvalidateUserTokensAsync(userId, ct);
        return NoContent();
    }

    [HttpDelete("roles")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> RemoveRole(
        Guid userId,
        [FromBody] ScopedRoleRequest request,
        CancellationToken ct)
    {
        var (success, error) = await scopedPermissions.RemoveRoleAsync(
            userId, request.RoleId, request.ScopeType, request.ScopeId, ct);

        if (!success)
            return BadRequest(new { message = error });

        await tokenInvalidation.InvalidateUserTokensAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("permissions")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> AssignPermission(
        Guid userId,
        [FromBody] ScopedPermissionRequest request,
        CancellationToken ct)
    {
        var grantedBy = GetCallerId();
        var (success, error) = await scopedPermissions.AssignPermissionAsync(
            userId,
            request.PermissionId,
            request.ScopeType,
            request.ScopeId,
            string.IsNullOrWhiteSpace(request.Effect) ? "Grant" : request.Effect,
            grantedBy,
            ct);

        if (!success)
            return BadRequest(new { message = error });

        await tokenInvalidation.InvalidateUserTokensAsync(userId, ct);
        return NoContent();
    }

    [HttpDelete("permissions")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> RemovePermission(
        Guid userId,
        [FromBody] ScopedPermissionRequest request,
        CancellationToken ct)
    {
        var (success, error) = await scopedPermissions.RemovePermissionAsync(
            userId, request.PermissionId, request.ScopeType, request.ScopeId, ct);

        if (!success)
            return BadRequest(new { message = error });

        await tokenInvalidation.InvalidateUserTokensAsync(userId, ct);
        return NoContent();
    }

    private Guid? GetCallerId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record ScopedRoleRequest(Guid RoleId, string ScopeType, Guid? ScopeId);

public record ScopedPermissionRequest(Guid PermissionId, string ScopeType, Guid? ScopeId, string? Effect);
