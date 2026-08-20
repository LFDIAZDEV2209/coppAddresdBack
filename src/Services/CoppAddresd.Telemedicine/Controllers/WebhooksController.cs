using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Recepción de webhooks del proveedor de video (Twilio). Sin autenticación JWT:
/// la legitimidad se valida con la firma del proveedor (X-Twilio-Signature)
/// dentro del handler. El procesamiento es idempotente (clave única por evento)
/// y atómico (una transacción por evento).
/// </summary>
[ApiController]
[Route("api/v1/telemedicine/webhooks")]
public class WebhooksController(IMediator mediator) : ControllerBase
{
    [HttpPost("twilio")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebhookProcessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebhookProcessResult>> Twilio(CancellationToken ct)
    {
        // URL absoluta tal como la construye Twilio: es parte de la firma.
        var url = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        var signature = Request.Headers["X-Twilio-Signature"].FirstOrDefault() ?? string.Empty;

        var formParams = new Dictionary<string, string>();
        foreach (var key in Request.Form.Keys)
        {
            formParams[key] = Request.Form[key].ToString();
        }

        var result = await mediator.Send(
            new ProcessTwilioWebhookCommand(url, signature, formParams), ct);

        return result.Outcome == WebhookProcessOutcome.InvalidSignature
            ? Unauthorized()
            : Ok(result);
    }
}
