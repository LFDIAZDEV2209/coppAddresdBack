using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Sonda ultraligera de conectividad para la cola offline del móvil
/// (Fase 12): sin autenticación (el chequeo debe funcionar con el token
/// expirado), sin BD y sin dependencias — 200 OK con la marca temporal UTC
/// del servidor.
/// </summary>
[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
