using CoppAddresd.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Notificaciones push (FCM). Registro/desregistro de tokens de dispositivos
/// (solo autenticación: el userId se deriva SIEMPRE del JWT) y disparo de
/// envíos desde el ERP (el userId destino viene en el body).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator) : ControllerBase
{
    /// <summary>Registra (upsert) el token de un dispositivo del usuario actual.</summary>
    [HttpPost("devices")]
    [ProducesResponseType(typeof(DeviceTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeviceTokenDto>> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "El token es requerido." });
        }

        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var deviceToken = await mediator.Send(
            new RegisterDeviceTokenCommand(userId, request.Token, request.Platform ?? string.Empty),
            ct);

        return Ok(deviceToken);
    }

    /// <summary>Desregistra el token de un dispositivo del usuario actual.</summary>
    [HttpDelete("devices/{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnregisterDevice(string token, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var removed = await mediator.Send(new UnregisterDeviceTokenCommand(userId, token), ct);
        if (!removed)
        {
            return NotFound(new { message = "Token no registrado." });
        }

        return NoContent();
    }

    /// <summary>
    /// Envía una notificación push (FCM) a todos los dispositivos del usuario
    /// indicado y coordina la inyección del mensaje en su chat (best-effort,
    /// sin LLM). El userId destino viene en el body, no del JWT: queda
    /// protegido por el <c>[Authorize]</c> del controlador (F2 — antes era
    /// <c>[AllowAnonymous]</c> y permitía push anónimos). Con FCM no
    /// configurado (Enabled=false) responde 200 con sentCount=0/disabledCount>0,
    /// sin romper el flujo.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendPushNotificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendPushNotificationResult>> SendNotification(
        [FromBody] SendPushNotificationRequest request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { message = "El userId es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "El título es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "El body es requerido." });
        }

        var result = await mediator.Send(
            new SendPushNotificationCommand(request.UserId, request.Title, request.Body, request.AgentTypeId),
            ct);

        return Ok(result);
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}