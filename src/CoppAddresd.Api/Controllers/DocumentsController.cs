using CoppAddresd.Api.Context;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Documents;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Repositorio documental de pacientes (Fase 5). Metadata + upload-intent con
/// URL firmada; el binario vive en el storage de objetos. La frontera de datos
/// replica el patrón de pacientes: con clínica activa (X-Clinic-Id) un
/// documento (o paciente) de otra clínica se trata como 404.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DocumentsController(
    IMediator mediator,
    ICurrentContext context,
    StorageSignatureService signatureService,
    IObjectStorageService objectStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedDocumentsResult>> List(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? documentTypeId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await context.HasPermissionAsync("Documents.View", ct))
            return Forbid();

        var result = await mediator.Send(new ListDocumentsQuery(
            patientId, context.ActiveClinicId, categoryId, documentTypeId, status, search, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<DocumentCatalogDto>> Catalog(CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.View", ct))
            return Forbid();

        return Ok(await mediator.Send(new ListDocumentCatalogQuery(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.View", ct))
            return Forbid();

        var document = await mediator.Send(new GetDocumentQuery(id), ct);
        if (document is null || IsOutsideActiveClinic(document))
            return NotFound(new { message = "Documento no encontrado" });

        return Ok(document);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<DocumentVersionDto>>> GetVersions(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.View", ct))
            return Forbid();

        var root = await mediator.Send(new GetDocumentQuery(id), ct);
        if (root is null || IsOutsideActiveClinic(root))
            return NotFound(new { message = "Documento no encontrado" });

        var rootId = root.ParentDocumentId ?? root.Id;
        return Ok(await mediator.Send(new ListDocumentVersionsQuery(rootId), ct));
    }

    /// <summary>
    /// URL firmada para descargar el binario. Con S3 es un presigned URL real
    /// del bucket; con el proveedor Local es el proxy
    /// (<c>GET /api/v1/storage/{{key}}?exp&amp;sig</c>) para que el navegador
    /// abra/descargue sin header Bearer.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<ActionResult<object>> Download(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.View", ct))
            return Forbid();

        var document = await mediator.Send(new GetDocumentQuery(id), ct);
        if (document is null || IsOutsideActiveClinic(document))
            return NotFound(new { message = "Documento no encontrado" });

        var expiresInSeconds = 900;
        if (objectStorage.IsCloudStorage)
        {
            var cloudUrl = await objectStorage.GetPreSignedUrlAsync(
                document.StorageKey, TimeSpan.FromSeconds(expiresInSeconds), ct);
            return Ok(new { url = cloudUrl, expiresInSeconds });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        var signature = signatureService.Sign(document.StorageKey, expiresAt);

        var url = $"{Request.Scheme}://{Request.Host}/api/v1/storage/{document.StorageKey}" +
                  $"?exp={expiresAt.ToUnixTimeSeconds()}&sig={signature}";

        return Ok(new { url, expiresInSeconds });
    }

    /// <summary>
    /// Intent de subida: valida paciente/tipo/extensión ANTES de subir el binario
    /// y devuelve la clave de storage + URL de escritura (PUT) firmada.
    /// </summary>
    [HttpPost("upload-intent")]
    public async Task<ActionResult<object>> CreateUploadIntent(
        [FromBody] CreateDocumentUploadIntentRequest request,
        CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.Upload", ct))
            return Forbid();

        if (await IsPatientOutsideActiveClinic(request.PatientId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        var result = await mediator.Send(new CreateDocumentUploadIntentCommand(
            request.PatientId,
            request.DocumentTypeId,
            request.FileName,
            request.ParentDocumentId), ct);

        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var presignedUrl = await objectStorage.GetPreSignedUploadUrlAsync(
            result.StorageKey, request.ContentType, TimeSpan.FromSeconds(result.ExpiresInSeconds), publicBaseUrl, ct);
        return Ok(new { result.StorageKey, presignedUrl, result.ExpiresInSeconds });
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Create(
        [FromBody] CreateDocumentRequest request,
        CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.Upload", ct))
            return Forbid();

        // Una versión nueva (parentDocumentId) no lleva PatientId: la clínica y
        // el paciente se heredan del documento raíz en el handler. Solo cuando
        // no hay parent se exige paciente (y la frontera de clínica activa).
        if (request.ParentDocumentId is null)
        {
            if (request.PatientId is not { } patientId || await IsPatientOutsideActiveClinic(patientId, ct))
                return NotFound(new { message = "Paciente no encontrado" });
        }
        else
        {
            var parent = await mediator.Send(new GetDocumentQuery(request.ParentDocumentId.Value), ct);
            if (parent is null || IsOutsideActiveClinic(parent))
                return NotFound(new { message = "Documento no encontrado" });
        }

        var command = new CreateDocumentCommand(
            request.PatientId,
            request.ProfessionalId,
            request.DocumentTypeId,
            request.Title,
            request.Description,
            request.StorageKey,
            request.ContentType,
            request.FileSizeBytes,
            request.ParentDocumentId,
            // La clínica se hereda del paciente (regla anti-tenancy); nunca del cuerpo.
            null,
            context.UserId,
            context.UserId);

        var document = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Update(
        Guid id,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.Update", ct))
            return Forbid();

        var current = await mediator.Send(new GetDocumentQuery(id), ct);
        if (current is null || IsOutsideActiveClinic(current))
            return NotFound(new { message = "Documento no encontrado" });

        var command = new UpdateDocumentCommand(
            id, request.Title, request.Description, request.DocumentTypeId, request.Status, context.UserId);

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Documento no encontrado" });

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Documents.Delete", ct))
            return Forbid();

        var current = await mediator.Send(new GetDocumentQuery(id), ct);
        if (current is null || IsOutsideActiveClinic(current))
            return NotFound(new { message = "Documento no encontrado" });

        var deleted = await mediator.Send(new DeleteDocumentCommand(id, context.UserId), ct);
        if (!deleted)
            return NotFound(new { message = "Documento no encontrado" });

        return NoContent();
    }

    /// <summary>Con clínica activa, un documento de otra clínica se trata como 404.</summary>
    private bool IsOutsideActiveClinic(DocumentDto document)
        => context.ActiveClinicId is { } clinicId && document.ClinicId != clinicId;

    /// <summary>Con clínica activa, un paciente de otra clínica se trata como 404 (anti-leak).</summary>
    private async Task<bool> IsPatientOutsideActiveClinic(Guid patientId, CancellationToken ct)
    {
        if (context.ActiveClinicId is null)
            return false;

        var patient = await mediator.Send(new GetPatientQuery(patientId), ct);
        return patient is null || patient.ClinicId != context.ActiveClinicId;
    }
}

public record CreateDocumentUploadIntentRequest(
    Guid PatientId,
    Guid DocumentTypeId,
    string FileName,
    Guid? ParentDocumentId,
    string? ContentType = null);

public record CreateDocumentRequest(
    Guid? PatientId,
    Guid? ProfessionalId,
    Guid DocumentTypeId,
    string Title,
    string? Description,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    Guid? ParentDocumentId);

public record UpdateDocumentRequest(
    string? Title,
    string? Description,
    Guid? DocumentTypeId,
    string? Status);