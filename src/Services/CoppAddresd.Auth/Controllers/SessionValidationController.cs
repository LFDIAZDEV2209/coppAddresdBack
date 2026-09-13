using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Controllers;

/// <summary>Introspección autenticada; JwtBearer ya comprueba suspensión y versión ERP.</summary>
[ApiController]
[Route("api/auth/session")]
[Authorize]
public sealed class SessionValidationController : ControllerBase
{
    [HttpGet("validate")]
    public IActionResult Validate() => NoContent();
}
