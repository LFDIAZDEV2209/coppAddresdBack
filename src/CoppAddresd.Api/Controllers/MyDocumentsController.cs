using CoppAddresd.Api.Context;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Documents;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>Documentos propios en Historia. El paciente se resuelve por el
/// usuario autenticado; nunca se acepta un patientId elegido por el cliente.</summary>
[ApiController, Authorize, Route("api/v1/me/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyDocumentsController(IMediator mediator, ICurrentContext context,
    IPatientRepository patients, IDocumentRepository documents,
    IObjectStorageService storage, StorageSignatureService signatures) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedDocumentsResult>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!User.HasClaim("aud", "app")) return Forbid();
        if (context.UserId is not { } userId) return Unauthorized();
        var patient = await patients.GetByUserIdAsync(userId, ct);
        if (patient is null || patient.DeletedAt is not null) return NotFound();
        return Ok(await mediator.Send(new ListDocumentsQuery(PatientId: patient.Id,
            Page: Math.Max(1, page), PageSize: Math.Clamp(pageSize, 1, 100)), ct));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<ActionResult<object>> Download(Guid id, CancellationToken ct)
    {
        if (!User.HasClaim("aud", "app")) return Forbid();
        if (context.UserId is not { } userId) return Unauthorized();
        var patient = await patients.GetByUserIdAsync(userId, ct);
        if (patient is null || patient.DeletedAt is not null) return NotFound();
        var document = await documents.GetByIdAsync(id, ct);
        if (document is null || document.DeletedAt is not null || document.PatientId != patient.Id) return NotFound();
        if (ArchivedDocumentAccess.IsArchiveKey(document.StorageKey)
            && await new ArchivedDocumentAccess(documents, patients, context)
                .ResolveReadableAsync(document.StorageKey, User, false, ct) is null) return NotFound();
        const int expiresInSeconds = 900;
        var expiry = TimeSpan.FromSeconds(expiresInSeconds);
        if (storage.IsCloudStorage)
            return Ok(new { url = ArchivedDocumentAccess.IsArchiveKey(document.StorageKey)
                ? await storage.GetDocumentArchiveReadUrlAsync(document.StorageKey, expiry, ct)
                : await storage.GetPreSignedUrlAsync(document.StorageKey, expiry, ct), expiresInSeconds });
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        var signature = signatures.Sign(document.StorageKey, expiresAt);
        var url = $"{Request.Scheme}://{Request.Host}/api/v1/storage/{document.StorageKey}?exp={expiresAt.ToUnixTimeSeconds()}&sig={signature}";
        return Ok(new { url, expiresInSeconds });
    }
}
