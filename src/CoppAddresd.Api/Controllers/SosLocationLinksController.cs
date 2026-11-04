using CoppAddresd.Application.Features.Sos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoppAddresd.Api.Controllers;

/// <summary>Acceso por posesión del enlace firmado que recibió el contacto de emergencia.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/sos/maps")]
[EnableRateLimiting("sos-location-links")]
public sealed class SosLocationLinksController(IMediator mediator) : ControllerBase
{
    [HttpGet("{token}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Open(string token, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, max-age=0";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        var destination = await mediator.Send(new ResolveSosLocationLinkQuery(token), ct);
        return destination is null ? NotFound() : Redirect(destination);
    }
}
