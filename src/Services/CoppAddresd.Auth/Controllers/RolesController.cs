using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/auth/roles")]
[Authorize]
[RequireErpAudience]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly IAuthorizationService _authorizationService;

    public RolesController(
        IRoleService roleService,
        IPermissionService permissionService,
        IAuthorizationService authorizationService
    )
    {
        _roleService = roleService;
        _permissionService = permissionService;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.RolesView)]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> GetAll(CancellationToken ct)
    {
        var roles = await _roleService.GetAllAsync(ct);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.RolesView)]
    public async Task<ActionResult<RoleResponse>> GetById(Guid id, CancellationToken ct)
    {
        var role = await _roleService.GetByIdAsync(id, ct);
        if (role is null)
            return NotFound(new { message = "Rol no encontrado" });

        return Ok(role);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.RolesCreate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para gestionar roles." }
            );

        var (success, error, role) = await _roleService.CreateAsync(request, ct);
        if (!success)
            return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = role!.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.RolesUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken ct
    )
    {
        var existing = await _roleService.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound(new { message = "Rol no encontrado" });

        var mutatesSystemRole =
            existing.IsSystem
            && (
                request.Name is not null && request.Name != existing.Name
                || request.IsActive.HasValue && request.IsActive.Value != existing.IsActive
            );

        if (mutatesSystemRole && !await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "No se puede renombrar o activar/desactivar un rol de sistema sin System.AdminSettings.",
                }
            );

        var (success, error) = await _roleService.UpdateAsync(
            id,
            request,
            await HasSystemAdminSettingsAsync(),
            ct
        );
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.RolesDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var existing = await _roleService.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound(new { message = "Rol no encontrado" });

        if (existing.IsSystem && !await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "No se puede eliminar un rol de sistema sin System.AdminSettings." }
            );

        var (success, error) = await _roleService.DeleteAsync(
            id,
            await HasSystemAdminSettingsAsync(),
            ct
        );
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    [HttpGet("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.RolesView)]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> GetUserRoles(
        Guid userId,
        CancellationToken ct
    )
    {
        var roles = await _roleService.GetUserRolesAsync(userId, ct);
        return Ok(roles);
    }

    [HttpPut("{roleId:guid}/permissions")]
    [RequirePermission(PermissionCodes.PermissionsAssign)]
    public async Task<IActionResult> SyncPermissions(
        Guid roleId,
        [FromBody] SyncRolePermissionsRequest request,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para asignar permisos." }
            );

        var (success, error) = await _permissionService.SetForRoleAsync(
            roleId,
            request.PermissionIds,
            ct
        );
        if (!success)
            return BadRequest(new { message = error });

        return NoContent();
    }

    [HttpPost("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> AssignToUser(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct
    )
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para asignar roles." }
            );

        var (success, error) = await _roleService.AssignToUserAsync(userId, request.RoleId, ct);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Rol asignado correctamente" });
    }

    [HttpDelete("user/{userId:guid}/{roleId:guid}")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> RemoveFromUser(Guid userId, Guid roleId, CancellationToken ct)
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para remover roles." }
            );

        var (success, error) = await _roleService.RemoveFromUserAsync(userId, roleId, ct);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Rol removido correctamente" });
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
