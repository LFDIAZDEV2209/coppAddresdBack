using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.WhatsApp.Webhook;

[ApiController]
[AllowAnonymous]
[Route("api/v1/whatsapp/webhook")]
public sealed class WhatsAppWebhookController(IServiceProvider services) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var webhook = services.GetService<WhatsAppWebhook>();
        if (webhook is null) return NotFound();
        return webhook.Verify(mode, token) && challenge is not null ? Content(challenge, "text/plain") : StatusCode(403);
    }

    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var webhook = services.GetService<WhatsAppWebhook>();
        if (webhook is null) return NotFound();
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        return StatusCode(await webhook.AcceptAsync(body.ToArray(), Request.Headers["X-Hub-Signature-256"].ToString(), ct));
    }
}
