using CoppAddresd.Api.Authorization;
using CoppAddresd.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Entrega de notificaciones internas para el microservicio de Telemedicina
/// (F2): <c>POST /api/v1/internal/telemedicine/notifications</c> con header
/// <c>X-Internal-Key</c> (mismo patrón que
/// <see cref="AppointmentReferenceController"/>, clave
/// <c>Telemedicine:InternalApiKey</c>). Sin JWT: la credencial es la clave
/// interna servicio-a-servicio.
///
/// El micro decide QUÉ/CUÁNDO notificar; esta API resuelve destinatario, hace
/// el fan-out push (FCM) y el SMS, y responde 200 con el estado por canal
/// (<c>sent | skipped | failed | disabled</c>). Los fallos de proveedor NUNCA
/// devuelven 5xx: solo 400 por payload inválido y 401 por clave inválida.
/// </summary>
[ApiController]
[Route("api/v1/internal/telemedicine")]
[RequireInternalKey]
public class InternalTelemedicineNotificationsController(IMediator mediator) : ControllerBase
{
    [HttpPost("notifications")]
    [ProducesResponseType(typeof(SendTelemedicineNotificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendTelemedicineNotificationResult>> SendNotification(
        [FromBody] SendTelemedicineNotificationRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new SendTelemedicineNotificationCommand(
                request.UserId,
                request.Title,
                request.Body,
                request.Channels,
                request.Data,
                request.DedupeKey),
            ct);

        return Ok(result);
    }
}
