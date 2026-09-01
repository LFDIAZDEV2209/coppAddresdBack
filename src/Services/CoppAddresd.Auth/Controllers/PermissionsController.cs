using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/auth/permissions")]
[Authorize]
[RequireErpAudience]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IAuthorizationService _authorizationService;

    public PermissionsController(
        IPermissionService permissionService,
        IAuthorizationService authorizationService
    )
    {
        _permissionService = permissionService;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.PermissionsView)]
    public async Task<ActionResult<IEnumerable<PermissionResponse>>> GetAll(CancellationToken ct)
    {
        var permissions = await _permissionService.GetAllAsync(ct);
        return Ok(permissions);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.PermissionsView)]
    public async Task<ActionResult<PermissionResponse>> GetById(Guid id, CancellationToken ct)
    {
        var permission = await _permissionService.GetByIdAsync(id, ct);
        if (permission is null)
            return NotFound(new { message = "Permiso no encontrado" });

        return Ok(permission);
    }

    [HttpGet("code/{code}")]
    [RequirePermission(PermissionCodes.PermissionsView)]
    public async Task<ActionResult<PermissionResponse>> GetByCode(string code, CancellationToken ct)
    {
        var permission = await _permissionService.GetByCodeAsync(code, ct);
        if (permission is null)
            return NotFound(new { message = "Permiso no encontrado" });

        return Ok(permission);
    }

    [HttpGet("role/{roleId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsView)]
    public async Task<ActionResult<IEnumerable<PermissionResponse>>> GetRolePermissions(
        Guid roleId,
        CancellationToken ct
    )
    {
        var permissions = await _permissionService.GetRolePermissionsAsync(roleId, ct);
        return Ok(permissions);
    }

    [HttpPost("role/{roleId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> AssignToRole(
        Guid roleId,
        [FromBody] AssignPermissionToRoleRequest request,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para asignar permisos." }
            );

        var (success, error) = await _permissionService.AssignToRoleAsync(
            roleId,
            request.PermissionId,
            ct
        );
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Permiso asignado al rol correctamente" });
    }

    [HttpDelete("role/{roleId:guid}/{permissionId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> RemoveFromRole(
        Guid roleId,
        Guid permissionId,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para remover permisos." }
            );

        var (success, error) = await _permissionService.RemoveFromRoleAsync(
            roleId,
            permissionId,
            ct
        );
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Permiso removido del rol correctamente" });
    }

    [HttpGet("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsView)]
    public async Task<ActionResult<IEnumerable<PermissionResponse>>> GetUserPermissions(
        Guid userId,
        CancellationToken ct
    )
    {
        var permissions = await _permissionService.GetUserPermissionsAsync(userId, ct);
        return Ok(permissions);
    }

    [HttpPost("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> AssignToUser(
        Guid userId,
        [FromBody] AssignPermissionToUserRequest request,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para asignar permisos." }
            );

        var (success, error) = await _permissionService.AssignToUserAsync(
            userId,
            request.PermissionId,
            ct
        );
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Permiso asignado al usuario correctamente" });
    }

    [HttpDelete("user/{userId:guid}/{permissionId:guid}")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> RemoveFromUser(
        Guid userId,
        Guid permissionId,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para remover permisos." }
            );

        var (success, error) = await _permissionService.RemoveFromUserAsync(
            userId,
            permissionId,
            ct
        );
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Permiso removido del usuario correctamente" });
    }

    /// <summary>¿El caller tiene System.AdminSettings (configuración crítica)?</summary>
    private async Task<bool> HasSystemAdminSettingsAsync()
    {
        var result = await _authorizationService.AuthorizeAsync(
            User,
            PermissionCodes.SystemAdminSettings
        );
        return result.Succeeded;
    }
}
