using System.Security.Claims;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/auth/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPermissionService _permissionService;
    private readonly IUserPreferenceService _userPreferenceService;

    public MeController(
        IUserService userService,
        IPermissionService permissionService,
        IUserPreferenceService userPreferenceService
    )
    {
        _userService = userService;
        _permissionService = permissionService;
        _userPreferenceService = userPreferenceService;
    }

    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUserInfo(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { message = "Token inválido" });
        }

        var user = await _userService.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }

        // Permisos efectivos (incluye roles con scope) para gating de UI.
        // El enforcement real es server-side por request; aqui solo se decide
        // que mostrar en el menu. Los roles con scope se agregan a la lista de
        // roles para que hasRole() tambien los vea.
        var permissions = await _permissionService.GetUserEffectivePermissionCodesAsync(userId, ct);
        var scopedRoleNames = (user.ScopedRoles ?? [])
            .Select(r => r.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var roles = user.Roles
            .Concat(scopedRoleNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(
            new CurrentUserResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                roles,
                permissions.ToArray()
            )
        );
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<UserPreferenceResponse>> GetPreferences(CancellationToken ct)
    {
        var prefs = await _userPreferenceService.GetByUserIdAsync(GetUserId(), ct);
        return Ok(new UserPreferenceResponse(prefs?.Lang, prefs?.AccentColor));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateUserPreferenceRequest request,
        CancellationToken ct
    )
    {
        var lang = request.Lang;
        var accentColor = request.AccentColor;

        if (lang is not null && lang != "es" && lang != "en")
            return BadRequest(new { message = "Lang must be 'es' or 'en'" });
        if (
            accentColor is not null
            && !System.Text.RegularExpressions.Regex.IsMatch(accentColor, "^#[0-9a-fA-F]{6}$")
        )
            return BadRequest(new { message = "AccentColor must be a valid hex color (#RRGGBB)" });
        if (lang is null && accentColor is null)
            return BadRequest(
                new { message = "At least one preference (lang, accentColor) is required" }
            );

        await _userPreferenceService.UpsertAsync(GetUserId(), lang, accentColor, ct);
        return Ok(new { message = "Preferences updated" });
    }
}

public record CurrentUserResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string[] Roles,
    string[] Permissions
);
