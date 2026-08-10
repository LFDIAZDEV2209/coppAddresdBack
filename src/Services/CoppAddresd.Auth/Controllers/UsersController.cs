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
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var (success, error, user) = await _userService.CreateAsync(request, ct);
        if (!success)
            return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = user!.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        var (success, error) = await _userService.UpdateAsync(id, request, ct);
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.UsersDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (success, error) = await _userService.DeleteAsync(id, ct);
        if (!success)
            return NotFound(new { message = error });

        return NoContent();
    }
}
