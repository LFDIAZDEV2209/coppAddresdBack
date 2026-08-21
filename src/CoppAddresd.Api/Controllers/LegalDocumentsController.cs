using CoppAddresd.Application.Features.LegalDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/legal-documents")]
[Authorize]
public sealed class LegalDocumentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentSummaryDto>>> List(CancellationToken ct)
        => Ok(await mediator.Send(new ListLegalDocumentsQuery(), ct));

    [HttpGet("all-versions")]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentVersionListDto>>> AllVersions(CancellationToken ct)
        => Ok(await mediator.Send(new ListAllLegalDocumentVersionsQuery(), ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<LegalDocumentDetailDto>> Get(string code, CancellationToken ct)
    {
        var result = await mediator.Send(new GetLegalDocumentQuery(code), ct);
        return result is null ? NotFound(new { message = "Documento legal no encontrado." }) : Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("public/{code}")]
    public async Task<ActionResult<PublishedLegalDocumentDto>> GetPublished(string code, CancellationToken ct)
    {
        var result = await mediator.Send(new GetLegalDocumentQuery(code), ct);
        if (result is null || !result.IsPublished || result.CurrentVersion is null)
            return NotFound(new { message = "Documento legal publicado no encontrado." });

        var current = result.Versions.FirstOrDefault(version => version.IsCurrent);
        return Ok(new PublishedLegalDocumentDto(
            result.Code,
            result.Title,
            current?.Content ?? string.Empty,
            result.CurrentVersion,
            result.UpdatedAt ?? DateTime.UtcNow));
    }

    [HttpGet("{code}/versions")]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentVersionDto>>> Versions(
        string code, CancellationToken ct)
        => Ok(await mediator.Send(new ListLegalDocumentVersionsQuery(code), ct));

    [HttpPost("{code}/drafts")]
    public async Task<ActionResult<LegalDocumentDetailDto>> SaveDraft(
        string code, [FromBody] SaveDraftRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new SaveDraftCommand(code, request), ct));

    [HttpPost("{code}/publish")]
    public async Task<ActionResult<LegalDocumentDetailDto>> Publish(
        string code, [FromBody] PublishRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new PublishCommand(code, request), ct));
}
