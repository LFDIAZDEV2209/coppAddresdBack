using System.Security.Claims;
using CoppAddresd.Auth.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPermissionService _permissionService;

    public MeController(IUserService userService, IPermissionService permissionService)
    {
        _userService = userService;
        _permissionService = permissionService;
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
}

public record CurrentUserResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string[] Roles,
    string[] Permissions);
