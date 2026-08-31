using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/auth/users")]
[Authorize]
[RequireErpAudience]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;

    public UsersController(IUserService userService, IAuthorizationService authorizationService)
    {
        _userService = userService;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.UsersView)]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll(CancellationToken ct)
    {
        var users = await _userService.GetAllAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersView)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado" });

        return Ok(user);
    }

    /// <summary>
    /// Crea un usuario (registro público, AllowAnonymous). Si el request trae
    /// roles/permisos a asignar, se exige caller autenticado con
    /// Users.Create + Roles.Assign + Permissions.Assign: jamás se aplican
    /// asignaciones en silencio a un caller anónimo (eso permitiría crear
    /// admins sin login). Rate limiting por IP (policy "auth") para acotar el
    /// spam de cuentas en el registro público.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct
    )
    {
        var wantsAssignments =
            request.RoleIds is { Length: > 0 } || request.PermissionIds is { Length: > 0 };
        if (wantsAssignments)
        {
            var authorized = await HasAssignPermissionsAsync(requireUsersCreate: true);
            if (!authorized)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "No tienes permisos para asignar roles y permisos al crear un usuario. Iniciá sesión con una cuenta con Users.Create, Roles.Assign y Permissions.Assign.",
                    }
                );
            }

            if (!await HasSystemAdminSettingsAsync())
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Se requiere System.AdminSettings para asignar roles y permisos al crear un usuario.",
                    }
                );
            }
        }

        var (success, error, user) = await _userService.CreateAsync(request, ct);
        if (!success)
            return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = user!.Id }, user);
    }

    /// <summary>
    /// Actualiza el perfil del usuario. Si el request envía RoleIds o
    /// PermissionIds (sync total), se exige además Roles.Assign y
    /// Permissions.Assign. "Usuario no encontrado" → 404; los errores de
    /// payload (rol/permiso inexistente, validación) → 400.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct
    )
    {
        var wantsAssignments = request.RoleIds is not null || request.PermissionIds is not null;
        if (wantsAssignments)
        {
            var authorized = await HasAssignPermissionsAsync();
            if (!authorized)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "No tienes permisos para asignar roles y permisos al actualizar el usuario. Se requiere Roles.Assign y Permissions.Assign.",
                    }
                );
            }

            if (!await HasSystemAdminSettingsAsync())
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Se requiere System.AdminSettings para asignar roles y permisos al actualizar el usuario.",
                    }
                );
            }
        }

        var (success, error, notFound) = await _userService.UpdateAsync(id, request, ct);
        if (!success)
            return notFound
                ? NotFound(new { message = error })
                : BadRequest(new { message = error });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await HasSystemAdminSettingsAsync())
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Se requiere System.AdminSettings para eliminar usuarios." }
            );

        var (success, error) = await _userService.DeleteAsync(id, ct);
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    /// <summary>
    /// Exige Roles.Assign y Permissions.Assign vía el mecanismo de policies
    /// del proyecto (claims emitidos en el token). Con `requireUsersCreate`
    /// exige también Users.Create (caso del POST con asignaciones).
    /// Devuelve false si el caller es anónimo o le falta cualquiera de los
    /// permisos.
    /// </summary>
    private async Task<bool> HasAssignPermissionsAsync(bool requireUsersCreate = false)
    {
        if (requireUsersCreate)
        {
            var usersResult = await _authorizationService.AuthorizeAsync(
                User,
                PermissionCodes.UsersCreate
            );
            if (!usersResult.Succeeded)
            {
                return false;
            }
        }

        var rolesResult = await _authorizationService.AuthorizeAsync(
            User,
            PermissionCodes.RolesAssign
        );
        if (!rolesResult.Succeeded)
        {
            return false;
        }

        var permissionsResult = await _authorizationService.AuthorizeAsync(
            User,
            PermissionCodes.PermissionsAssign
        );
        return permissionsResult.Succeeded;
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
