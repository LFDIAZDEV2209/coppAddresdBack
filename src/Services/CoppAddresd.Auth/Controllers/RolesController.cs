using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
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
    public async Task<ActionResult<RoleResponse>> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken ct)
    {
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
        CancellationToken ct)
    {
        var (success, error) = await _roleService.UpdateAsync(id, request, ct);
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.RolesDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (success, error) = await _roleService.DeleteAsync(id, ct);
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    [HttpGet("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.RolesView)]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> GetUserRoles(Guid userId, CancellationToken ct)
    {
        var roles = await _roleService.GetUserRolesAsync(userId, ct);
        return Ok(roles);
    }

    [HttpPost("user/{userId:guid}")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> AssignToUser(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct)
    {
        var (success, error) = await _roleService.AssignToUserAsync(userId, request.RoleId, ct);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Rol asignado correctamente" });
    }

    [HttpDelete("user/{userId:guid}/{roleId:guid}")]
    [RequirePermission(PermissionCodes.RolesAssign)]
    public async Task<IActionResult> RemoveFromUser(Guid userId, Guid roleId, CancellationToken ct)
    {
        var (success, error) = await _roleService.RemoveFromUserAsync(userId, roleId, ct);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Rol removido correctamente" });
    }
}
