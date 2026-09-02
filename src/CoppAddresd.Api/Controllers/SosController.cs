using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Sos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoint SOS para activación de alertas de emergencia desde la app móvil.
/// El patientId se resuelve SIEMPRE del JWT (anti-IDOR): sin perfil de
/// paciente → 404.
/// </summary>
[ApiController]
[Route("api/v1/sos")]
[Authorize]
public class SosController(
    IMediator mediator,
    IProgramActorContext actorContext) : ControllerBase
{
    /// <summary>
    /// Activa una alerta SOS: resuelve el paciente del JWT, genera el
    /// bloque de datos de emergencia y dispara SMS + email + llamada de
    /// voz al contacto de emergencia (best-effort por canal).
    /// </summary>
    [HttpPost("alerts")]
    [ProducesResponseType(typeof(SosAlertResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SosAlertResult>> ActivateAlert(
        [FromBody] ActivateSosAlertRequest request,
        CancellationToken ct)
    {
        // ── Resolución del patientId desde el JWT (anti-IDOR) ──
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "No existe un perfil de paciente para el usuario autenticado." });
        }

        // ── Validación: lat/lng vienen en par ──
        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            return BadRequest(new { message = "Latitud y longitud deben proporcionarse juntas." });
        }

        // ── Validación: idioma soportado ──
        if (request.Language is not ("es" or "en"))
        {
            return BadRequest(new { message = "El idioma debe ser 'es' o 'en'." });
        }

        var result = await mediator.Send(
            new ActivateSosAlertCommand(patientId.Value, request),
            ct);

        if (result is null)
        {
            return NotFound(new { message = "No existe un perfil de paciente para el usuario autenticado." });
        }

        return Ok(result);
    }
}
