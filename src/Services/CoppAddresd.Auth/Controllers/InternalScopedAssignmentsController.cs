using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Gestión de asignaciones scoped (roles + overrides Grant/Deny por contexto)
/// para el ERP vía clave interna (<c>X-Internal-Key</c>). El ERP orquesta la
/// creación del profesional y la gestión de scopes desde el detalle con estos
/// endpoints; así el frontend nunca habla con el Auth Service directamente
/// para permisos por clínica.
/// </summary>
[ApiController]
[Route("api/auth/internal/scoped-assignments")]
[AllowAnonymous]
[RequireInternalKey]
public class InternalScopedAssignmentsController(IScopedPermissionService scopedPermissions) : ControllerBase
{
    /// <summary>Todas las asignaciones scoped del usuario (roles + overrides).</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Get([FromQuery] Guid userId, CancellationToken ct)
    {
        var snapshot = await scopedPermissions.GetAssignmentsAsync(userId, ct);

        return Ok(new
        {
            roles = snapshot.Roles.Select(r => new { r.RoleId, r.RoleName, r.ScopeType, r.ScopeId }),
            permissions = snapshot.Permissions.Select(p => new
            {
                p.PermissionId,
                p.PermissionCode,
                p.ScopeType,
                p.ScopeId,
                p.Effect,
            }),
        });
    }

    /// <summary>
    /// Reemplazo atómico de las asignaciones scoped del usuario (transacción en
    /// el Auth). El ERP lo usa para la creación del profesional y para la
    /// gestión de scopes por clínica.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Replace(
        [FromBody] ReplaceScopedAssignmentsRequest request,
        CancellationToken ct)
    {
        var (success, error) = await scopedPermissions.ReplaceAssignmentsAsync(
            request.UserId,
            request.Roles,
            request.Permissions,
            request.GrantedBy,
            ct);

        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { success = true });
    }
}

/// <summary>Request de reemplazo de asignaciones scoped.</summary>
public record ReplaceScopedAssignmentsRequest(
    Guid UserId,
    IReadOnlyList<ScopedRoleInput> Roles,
    IReadOnlyList<ScopedPermissionInput> Permissions,
    Guid? GrantedBy = null);