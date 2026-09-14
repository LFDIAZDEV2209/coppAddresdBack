using System.Security.Claims;
using CoppAddresd.Auth.Avatar.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

[ApiController, Authorize, Route("api/auth/me/avatar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AvatarConfigurationController(AvatarConfigurationUseCases useCases) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AvatarConfiguration>> Get(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        return Ok(await useCases.GetAsync(userId, ct));
    }

    [HttpPut, RequestSizeLimit(4096)]
    public async Task<ActionResult<AvatarConfiguration>> Put([FromBody] AvatarConfiguration configuration, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (!await useCases.PutAsync(userId, configuration, ct))
            return BadRequest(new { message = "Configuración de avatar o combinación de elementos no válida." });
        return Ok(configuration);
    }
}
