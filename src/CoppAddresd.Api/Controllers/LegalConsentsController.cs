using System.Security.Claims;
using CoppAddresd.Application.Features.LegalDocuments;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/me/legal-consents")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LegalConsentsController(ILegalConsentService consents) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<ActionResult<LegalConsentStatusDto>> Status([FromQuery] string locale = "es", CancellationToken ct = default)
        => UserId == Guid.Empty ? Unauthorized() : Ok(await consents.GetStatusAsync(UserId, locale, ct));

    [HttpPost("acceptances")]
    public async Task<ActionResult<LegalConsentReceiptDto>> Accept([FromBody] AcceptLegalConsentRequest request, CancellationToken ct)
        => UserId == Guid.Empty ? Unauthorized() : Ok(await consents.AcceptAsync(UserId, request, ct));

    [HttpDelete("acceptances/{id:guid}")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        await consents.WithdrawAsync(UserId, id, ct);
        return NoContent();
    }
}
