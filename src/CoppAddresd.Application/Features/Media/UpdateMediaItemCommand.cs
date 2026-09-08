using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Actualiza un medio existente. Devuelve <c>null</c> si no existe.</summary>
public record UpdateMediaItemCommand(
    Guid Id,
    string Title,
    string? Description,
    string Author,
    MediaType MediaType,
    MediaCategory Category,
    string StorageKey,
    string? ThumbnailKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    int Day,
    int Month,
    Guid? UpdatedBy = null,
    IReadOnlyList<MediaChapterDto>? Chapters = null,
    IReadOnlyList<string>? Takeaways = null)
    : IRequest<MediaItemDto?>;

public sealed class UpdateMediaItemCommandHandler(
    IMediaItemRepository repository,
    IObjectStorageService objectStorage,
    ILogger<UpdateMediaItemCommandHandler> logger) : IRequestHandler<UpdateMediaItemCommand, MediaItemDto?>
{
    public async Task<MediaItemDto?> Handle(UpdateMediaItemCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        var oldStorageKey = entity.StorageKey;
        var oldThumbnailKey = entity.ThumbnailKey;

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Author = request.Author;
        entity.MediaType = request.MediaType;
        entity.Category = request.Category;
        entity.StorageKey = request.StorageKey;
        entity.ThumbnailKey = request.ThumbnailKey;
        entity.ContentType = request.ContentType;
        entity.FileSizeBytes = request.FileSizeBytes;
        entity.DurationSecs = request.DurationSecs;
        entity.SortOrder = request.SortOrder;
        entity.Day = request.Day;
        entity.Month = request.Month;
        if (request.Chapters is not null)
            entity.Chapters = request.Chapters.ToList();
        if (request.Takeaways is not null)
            entity.Takeaways = request.Takeaways.ToList();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Transición a Published: fija PublishedAt si aún no estaba publicado.
        // Salida de Published: conserva el histórico en PublishedAt.
        if (request.Status == MediaStatus.Published && entity.PublishedAt is null)
        {
            entity.PublishedAt = DateTimeOffset.UtcNow;
        }

        entity.Status = request.Status;

        await repository.UpdateAsync(entity, ct);

        // Estrategia B: el objeto nuevo ya quedó subido por el front (presigned
        // PUT) antes de este update; acá solo se limpia el objeto viejo si la
        // clave cambió (reemplazo de archivo/miniatura). Un fallo de limpieza
        // no aborta la operación: el huérfano es basura acumulable, no pérdida.
        await DeleteIfChangedAsync(oldStorageKey, request.StorageKey, ct);
        await DeleteIfChangedAsync(oldThumbnailKey, request.ThumbnailKey, ct);

        logger.LogInformation("MediaItem updated: {Id} ({Title})", entity.Id, entity.Title);
        return MediaItemDto.FromEntity(entity);
    }

    private async Task DeleteIfChangedAsync(string? oldKey, string? newKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldKey) || string.Equals(oldKey, newKey, StringComparison.Ordinal))
            return;

        try
        {
            await objectStorage.DeleteObjectAsync(oldKey, ct);
            logger.LogInformation("Objeto de storage eliminado por reemplazo: {Key}", oldKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar el objeto de storage huérfano: {Key}", oldKey);
        }
    }
}
