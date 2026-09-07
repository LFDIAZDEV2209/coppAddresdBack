using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MediaController(
    IMediator mediator,
    IObjectStorageService objectStorage,
    StorageSignatureService? signatureService = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MediaItemDto>>> List(
        [FromQuery] MediaType? mediaType,
        [FromQuery] MediaStatus? status,
        CancellationToken ct)
    {
        var items = await mediator.Send(new ListMediaItemsQuery(mediaType, status), ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaItemDto>> GetById(Guid id, CancellationToken ct)
    {
        var item = await mediator.Send(new GetMediaItemQuery(id), ct);
        if (item is null)
            return NotFound(new { message = "Medio no encontrado" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<MediaItemDto>> Create(
        [FromBody] CreateMediaItemRequest request,
        CancellationToken ct)
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
            request.Takeaways);

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
    public async Task<ActionResult<UploadIntentResponse>> CreateUploadIntent(
        [FromBody] CreateUploadIntentRequest request,
        CancellationToken ct)
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
                return BadRequest(new { message = "La miniatura debe ser una imagen (Content-Type image/*)." });
            folder = "thumbnails";
        }
        else
        {
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "El contenido principal debe ser audio o video; las imágenes solo se admiten como miniatura." });
            folder = contentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase) ? "audio"
                : contentType.StartsWith("video", StringComparison.OrdinalIgnoreCase) ? "videos"
                : "podcasts";
        }

        var storageKey = $"media/{folder}/{Guid.NewGuid():N}{extension}";

        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var expiresIn = (int)TimeSpan.FromMinutes(15).TotalSeconds;

        var presignedUrl = await objectStorage.GetPreSignedUploadUrlAsync(
            storageKey, contentType, TimeSpan.FromSeconds(expiresIn), publicBaseUrl, ct);

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
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMediaItemRequest request,
        CancellationToken ct)
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
            request.Takeaways);

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Medio no encontrado" });

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteMediaItemCommand(id), ct);
        if (!deleted)
            return NotFound(new { message = "Medio no encontrado" });

        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
