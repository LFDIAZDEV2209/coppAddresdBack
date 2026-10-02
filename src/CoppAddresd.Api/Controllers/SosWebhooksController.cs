using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Webhooks de estado de Twilio para el SOS (voz y SMS). Twilio no envía JWT:
/// la autenticidad se valida con la firma <c>X-Twilio-Signature</c> dentro del
/// procesador. La URL firmada se reconstruye con <c>SosWebhook:BaseUrl</c> (la
/// misma base con la que se registró el callback), de modo que no depende de
/// cabeceras reenviadas del túnel.
/// </summary>
[ApiController]
[Route("api/v1/sos/webhooks")]
public sealed class SosWebhooksController(
    ISosWebhookProcessor processor,
    IOptions<SosWebhookSettings> webhookSettings
) : ControllerBase
{
    /// <summary>StatusCallback de la llamada TTS al contacto de emergencia.</summary>
    [HttpPost("twilio/voice")]
    [AllowAnonymous]
    public Task<IActionResult> TwilioVoice(CancellationToken ct) => HandleAsync(isVoice: true, ct);

    /// <summary>StatusCallback de entrega del SMS al contacto de emergencia.</summary>
    [HttpPost("twilio/sms")]
    [AllowAnonymous]
    public Task<IActionResult> TwilioSms(CancellationToken ct) => HandleAsync(isVoice: false, ct);

    private async Task<IActionResult> HandleAsync(bool isVoice, CancellationToken ct)
    {
        var baseUrl = webhookSettings.Value.NormalizedBaseUrl;
        if (baseUrl is null)
        {
            // Sin URL pública configurada no se emiten callbacks: nada que atender.
            return NotFound();
        }

        var url = $"{baseUrl}{Request.Path}{Request.QueryString}";
        var signature = Request.Headers["X-Twilio-Signature"].FirstOrDefault() ?? string.Empty;

        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in Request.Form.Keys)
        {
            form[key] = Request.Form[key].ToString();
        }

        var alertIdRaw = Request.Query["alertId"].FirstOrDefault();
        var alertId = Guid.TryParse(alertIdRaw, out var parsed) ? parsed : (Guid?)null;

        var outcome = isVoice
            ? await processor.ProcessVoiceAsync(alertId, url, signature, form, ct)
            : await processor.ProcessSmsAsync(alertId, url, signature, form, ct);

        return outcome switch
        {
            SosWebhookOutcome.Processed => Ok(new { status = "processed" }),
            SosWebhookOutcome.InvalidSignature => Unauthorized(),
            SosWebhookOutcome.NotFound => NotFound(),
            _ => Ok(new { status = "ignored" }),
        };
    }
}
