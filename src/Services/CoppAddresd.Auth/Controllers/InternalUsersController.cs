using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Lectura interna del estado de una cuenta por correo (X-Internal-Key).
/// La consume el ERP en el preflight del alta para saber si un correo ya
/// tiene cuenta (y si está activa o ya tiene contraseña) sin que el ERP lea
/// <c>auth.users</c> directamente.
/// </summary>
[ApiController]
[Route("api/auth/internal/users")]
[AllowAnonymous]
[RequireInternalKey]
public class InternalUsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Estado de la cuenta con ese correo: <c>exists=false</c> cuando no hay
    /// ninguna. Cuenta existente pero inactiva o con contraseña cambia el
    /// flujo de vinculación (adoptExisting) del alta de empleados.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<object>> Lookup(
        [FromQuery] string email,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(new { exists = false, isActive = false, hasPassword = false });
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return Ok(new { exists = false, isActive = false, hasPassword = false });
        }

        return Ok(
            new
            {
                exists = true,
                isActive = user.IsActive,
                hasPassword = user.PasswordHash is not null,
            }
        );
    }
}
