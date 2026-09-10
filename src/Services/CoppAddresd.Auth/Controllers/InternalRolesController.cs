using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Endpoints internos de consulta de roles para el ERP (clave interna
/// <c>X-Internal-Key</c>). El ERP resuelve el id del rol por nombre para
/// automatizar la asignación de scopes por clínica.
/// </summary>
[ApiController]
[Route("api/auth/internal/roles")]
[AllowAnonymous]
[RequireInternalKey]
public class InternalRolesController(AuthDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Busca un rol por nombre (case-insensitive). Devuelve el id, nombre
    /// y estado de activación. Lo usa el ERP para resolver el rol
    /// "Professional" al sincronizar scopes por clínica.
    /// </summary>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<object>> GetByName(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "El nombre del rol es requerido." });

        var normalizedName = name.Trim().ToUpperInvariant();

        var role = await dbContext.Roles
            .Where(r => r.NormalizedName == normalizedName)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name!,
                isActive = r.IsActive,
            })
            .FirstOrDefaultAsync(ct);

        if (role is null)
            return NotFound(new { message = $"Rol '{name}' no encontrado." });

        return Ok(role);
    }
}
