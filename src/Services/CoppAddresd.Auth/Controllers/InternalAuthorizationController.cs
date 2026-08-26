using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Endpoints de introspección de autorización para el ERP (clave interna
/// <c>X-Internal-Key</c>). El ERP los usa para evaluar permisos por contexto
/// y para poblar el switcher de clínica; los resultados se cachean allí
/// keyed por security stamp.
/// </summary>
[ApiController]
[Route("api/auth/internal")]
[AllowAnonymous]
[RequireInternalKey]
public class InternalAuthorizationController(
    IScopedPermissionService scopedPermissions,
    AuthDbContext dbContext
) : ControllerBase
{
    /// <summary>¿Tiene el usuario el permiso en la cadena de scopes indicada?</summary>
    [HttpGet("authorize")]
    public async Task<ActionResult<object>> Authorize(
        [FromQuery] Guid userId,
        [FromQuery] string permissionCode,
        [FromQuery] string? scopes,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return BadRequest(new { message = "permissionCode es requerido." });

        var chain = ScopeChainParser.Parse(scopes);
        if (chain is null)
            return BadRequest(
                new
                {
                    message = "scopes debe contener una cadena válida (ej. Clinic:id|Organization:id|Global).",
                }
            );

        var allowed = await scopedPermissions.AuthorizeAsync(userId, permissionCode, chain, ct);

        return Ok(new { allowed, scopeChain = ScopeEntry.EncodeChain(chain) });
    }

    /// <summary>Códigos de permiso efectivos del usuario para la cadena de scopes.</summary>
    [HttpGet("scoped-permissions")]
    public async Task<ActionResult<object>> ScopedPermissions(
        [FromQuery] Guid userId,
        [FromQuery] string? scopes,
        CancellationToken ct
    )
    {
        var chain = ScopeChainParser.Parse(scopes);
        if (chain is null)
            return BadRequest(
                new
                {
                    message = "scopes debe contener una cadena válida (ej. Clinic:id|Organization:id|Global).",
                }
            );

        var permissions = await scopedPermissions.GetEffectivePermissionsAsync(userId, chain, ct);

        return Ok(new { permissions });
    }

    /// <summary>
    /// UserIds con el rol asignado (global o scoped). Lo usa el ERP para
    /// filtrar el directorio de profesionales por rol sin leer el schema
    /// auth. (Los roles viven en el Auth Service; el ERP nunca consulta
    /// auth.* directamente.)
    /// </summary>
    [HttpGet("users-by-role")]
    public async Task<ActionResult<object>> UsersByRole(
        [FromQuery] Guid roleId,
        CancellationToken ct
    )
    {
        var globalIds = await dbContext
            .UserRoles.Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var scopedIds = await dbContext
            .ScopedRoleAssignments.Where(a => a.RoleId == roleId)
            .Select(a => a.UserId)
            .ToListAsync(ct);

        var userIds = globalIds.Concat(scopedIds).Distinct().OrderBy(id => id).ToList();

        return Ok(new { userIds });
    }
}

/// <summary>Parsea la cadena de scopes del query string (Clinic:id|Organization:id|Global).</summary>
internal static class ScopeChainParser
{
    public static IReadOnlyList<ScopeEntry>? Parse(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
        {
            return [ScopeEntry.Global];
        }

        var entries = new List<ScopeEntry>();
        foreach (
            var part in scopes.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var pieces = part.Split(':', 2);
            var scopeType = pieces[0].Trim();

            if (!ScopeTypes.IsValid(scopeType))
            {
                return null;
            }

            Guid? scopeId = null;
            if (pieces.Length == 2)
            {
                if (!Guid.TryParse(pieces[1].Trim(), out var parsed))
                {
                    return null;
                }
                scopeId = parsed;
            }

            entries.Add(new ScopeEntry(scopeType, scopeId));
        }

        return entries;
    }
}
