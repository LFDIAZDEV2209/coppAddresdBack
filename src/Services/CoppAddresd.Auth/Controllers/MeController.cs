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

    public MeController(IUserService userService, IPermissionService permissionService, IUserPreferenceService userPreferenceService)
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

        var permissions = await _permissionService.GetUserAllPermissionCodesAsync(userId, ct);

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Roles,
            permissions.ToArray()));
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
        return Ok(new UserPreferenceResponse(prefs?.Lang));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUserPreferenceRequest request, CancellationToken ct)
    {
        if (request.Lang != "es" && request.Lang != "en")
            return BadRequest(new { message = "Lang must be 'es' or 'en'" });

        await _userPreferenceService.UpsertAsync(GetUserId(), request.Lang, ct);
        return Ok(new { message = "Preferences updated" });
    }
}

public record CurrentUserResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string[] Roles,
    string[] Permissions);
