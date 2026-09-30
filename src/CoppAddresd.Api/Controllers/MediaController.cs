using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Biblioteca de medios (podcasts del programa de 83 días) administrada desde
/// el ERP. [Authorize] a nivel de controlador; cada acción declara su permiso
/// con <c>[RequirePermission("Media.X")]</c> (matriz de autorización del change
/// erp-program-content-admin: ver medios → Media.View, crear/subir →
/// Media.Create, editar/reordenar → Media.Edit, publicar → Media.Publish,
/// archivar → Media.Archive, eliminar → Media.Delete).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MediaController(
    IMediator mediator,
    IObjectStorageService objectStorage,
    StorageSignatureService? signatureService = null
) : ControllerBase
{
    /// <summary>
    /// Listado paginado server-side de la biblioteca (REQ-PCA-06): búsqueda
    /// textual, filtros combinados (mediaType/category/status/usage) y
    /// ordenación con whitelist. Envelope estándar
    /// <c>{ items, totalCount, page, pageSize, totalPages }</c> con
    /// <c>usageCount</c> por fila.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.MediaView)]
    public async Task<ActionResult<PagedMediaItemsResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] MediaType? mediaType = null,
        [FromQuery] MediaCategory? category = null,
        [FromQuery] MediaStatus? status = null,
        [FromQuery] string? usage = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken ct = default
    )
    {
        var result = await mediator.Send(
            new GetMediaItemsQuery(
                page,
                pageSize,
                search,
                mediaType,
                category,
                status,
                usage,
                sortBy,
                sortDirection
            ),
            ct
        );
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.MediaView)]
    public async Task<ActionResult<MediaItemDto>> GetById(Guid id, CancellationToken ct)
    {
        var item = await mediator.Send(new GetMediaItemQuery(id), ct);
        if (item is null)
            return NotFound(new { message = "Medio no encontrado" });

        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.MediaCreate)]
    public async Task<ActionResult<MediaItemDto>> Create(
        [FromBody] CreateMediaItemRequest request,
        CancellationToken ct
    )
    {
        var command = new CreateMediaItemCommand(
            request.Title,
            request.Description,
            request.Author,
            request.MediaType,
            request.Category,
            request.StorageKey,
            request.ThumbnailKey,
            request.ContentType,
            request.FileSizeBytes,
            request.DurationSecs,
            request.Status,
            request.SortOrder,
            request.Day,
            request.Month,
            CurrentUserId(),
            request.Chapters,
            request.Takeaways
        );

        var item = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>
    /// Genera una clave determinística y una URL firmada para subir el archivo
    /// directamente (front → storage). Con el proveedor Local la URL apunta a
    /// <c>PUT /api/v1/storage/{{key}}</c>; con S3 será un presigned URL real.
    /// El propósito <c>thumbnail</c> reserva la carpeta de imágenes de portada
    /// y exige Content-Type de imagen; el contenido principal solo admite
    /// audio/video.
    /// </summary>
    [HttpPost("upload-intent")]
    [RequirePermission(PermissionCodes.MediaCreate)]
    public async Task<ActionResult<UploadIntentResponse>> CreateUploadIntent(
        [FromBody] CreateUploadIntentRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { message = "El nombre del archivo es requerido." });

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var contentType = request.ContentType ?? "application/octet-stream";

        var purpose = request.Purpose?.Trim().ToLowerInvariant();
        string folder;
        if (purpose == "thumbnail")
        {
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(
                    new { message = "La miniatura debe ser una imagen (Content-Type image/*)." }
                );
            folder = "thumbnails";
        }
        else
        {
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(
                    new
                    {
                        message = "El contenido principal debe ser audio o video; las imágenes solo se admiten como miniatura.",
                    }
                );
            folder =
                contentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase) ? "audio"
                : contentType.StartsWith("video", StringComparison.OrdinalIgnoreCase) ? "videos"
                : "podcasts";
        }

        var storageKey = $"media/{folder}/{Guid.NewGuid():N}{extension}";

        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var expiresIn = (int)TimeSpan.FromMinutes(15).TotalSeconds;

        var presignedUrl = await objectStorage.GetPreSignedUploadUrlAsync(
            storageKey,
            contentType,
            TimeSpan.FromSeconds(expiresIn),
            publicBaseUrl,
            ct
        );

        if (!objectStorage.IsCloudStorage && signatureService is not null)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            var sig = signatureService.Sign(storageKey, expiresAt);
            var separator = presignedUrl.Contains('?') ? '&' : '?';
            presignedUrl += $"{separator}exp={expiresAt.ToUnixTimeSeconds()}&sig={sig}";
        }

        return Ok(new UploadIntentResponse(storageKey, presignedUrl, expiresIn));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.MediaEdit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMediaItemRequest request,
        CancellationToken ct
    )
    {
        var command = new UpdateMediaItemCommand(
            id,
            request.Title,
            request.Description,
            request.Author,
            request.MediaType,
            request.Category,
            request.StorageKey,
            request.ThumbnailKey,
            request.ContentType,
            request.FileSizeBytes,
            request.DurationSecs,
            request.Status,
            request.SortOrder,
            request.Day,
            request.Month,
            CurrentUserId(),
            request.Chapters,
            request.Takeaways
        );

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Medio no encontrado" });

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.MediaDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteMediaItemCommand(id), ct);
        if (result.NotFound)
            return NotFound(new { message = "Medio no encontrado" });

        // Guardia de integridad (REQ-PCA-07): con referencias activas → 409
        // Conflict con el detalle de las dependencias que bloquean la
        // eliminación (el front sugiere archivar en vez de eliminar).
        if (result.Blocked)
            return Conflict(
                new
                {
                    type = "https://httpstatuses.io/409",
                    title = "Conflict",
                    status = 409,
                    detail = "El medio no puede eliminarse porque tiene referencias activas "
                        + "(plantillas o semanas de pacientes). Sugerencia: archivar en lugar de eliminar.",
                    references = result.BlockingReferences,
                }
            );

        return NoContent();
    }

    // ===================== Acciones semánticas (REQ-PCA-03) =====================

    /// <summary>
    /// Publica un medio (Draft → Published) tras la validación técnica
    /// estricta del blob (REQ-PCA-02): existencia física, MIME permitido,
    /// tamaño y duración. Inconsistencia → 422 con ProblemDetails detallado.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [RequirePermission(PermissionCodes.MediaPublish)]
    public async Task<ActionResult<PublishMediaItemResult>> Publish(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new PublishMediaItemCommand(id), ct));

    /// <summary>
    /// Despublica un medio (Published → Draft) con registro auditable.
    /// Un medio Archived no se puede despublicar (422).
    /// </summary>
    [HttpPost("{id:guid}/unpublish")]
    [RequirePermission(PermissionCodes.MediaPublish)]
    public async Task<ActionResult<MediaItemDto>> Unpublish(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new UnpublishMediaItemCommand(id), ct));

    /// <summary>
    /// Archiva un medio (cualquier estado → Archived): sale de los selectores
    /// de nuevas asignaciones pero conserva su reproducción histórica.
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(PermissionCodes.MediaArchive)]
    public async Task<ActionResult<MediaItemDto>> Archive(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new ArchiveMediaItemCommand(id), ct));

    /// <summary>
    /// Reordenamiento en lote del catálogo (SortOrder), en una única
    /// escritura atómica. Body: <c>{ items: [{ id, sortOrder }] }</c>.
    /// </summary>
    [HttpPost("reorder")]
    [RequirePermission(PermissionCodes.MediaEdit)]
    public async Task<ActionResult<ReorderMediaItemsResult>> Reorder(
        [FromBody] ReorderMediaItemsCommand command,
        CancellationToken ct
    ) => Ok(await mediator.Send(command, ct));

    // ===================== Referencias y mantenimiento =====================

    /// <summary>
    /// "Dónde se usa" el medio (REQ-PCA-07): plantillas y semanas de pacientes
    /// que lo referencian. Lo consume el diálogo de dependencias del ERP y el
    /// manejo del 409 al eliminar.
    /// </summary>
    [HttpGet("{id:guid}/references")]
    [RequirePermission(PermissionCodes.MediaView)]
    public async Task<ActionResult<MediaReferencesDto>> GetReferences(Guid id, CancellationToken ct)
    {
        var references = await mediator.Send(new GetMediaReferencesQuery(id), ct);
        if (references is null)
            return NotFound(new { message = "Medio no encontrado" });

        return Ok(references);
    }

    /// <summary>
    /// Limpieza de huérfanos del storage (REQ-PCA-08): detecta (y con
    /// <c>dryRun=false</c> purga) los blobs bajo <c>media/</c> que no
    /// pertenecen a ningún medio registrado y superan la ventana de
    /// retención. Solo administradores (<c>System.AdminSettings</c>).
    /// </summary>
    [HttpPost("maintenance/cleanup-orphaned-blobs")]
    [RequirePermission(PermissionCodes.SystemAdminSettings)]
    public async Task<ActionResult<CleanupOrphanedBlobsResult>> CleanupOrphanedBlobs(
        [FromBody] CleanupOrphanedBlobsCommand command,
        CancellationToken ct
    ) => Ok(await mediator.Send(command, ct));

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
