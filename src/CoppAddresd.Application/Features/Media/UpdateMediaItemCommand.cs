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
    MediaType MediaType,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    Guid? UpdatedBy = null)
    : IRequest<MediaItemDto?>;

public sealed class UpdateMediaItemCommandHandler(
    IMediaItemRepository repository,
    ILogger<UpdateMediaItemCommandHandler> logger) : IRequestHandler<UpdateMediaItemCommand, MediaItemDto?>
{
    public async Task<MediaItemDto?> Handle(UpdateMediaItemCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.MediaType = request.MediaType;
        entity.StorageKey = request.StorageKey;
        entity.ContentType = request.ContentType;
        entity.FileSizeBytes = request.FileSizeBytes;
        entity.DurationSecs = request.DurationSecs;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Transición a Published: fija PublishedAt si aún no estaba publicado.
        // Salida de Published: conserva el histórico en PublishedAt.
        if (request.Status == MediaStatus.Published && entity.PublishedAt is null)
        {
            entity.PublishedAt = DateTimeOffset.UtcNow;
        }

        entity.Status = request.Status;

        await repository.UpdateAsync(entity, ct);

        logger.LogInformation("MediaItem updated: {Id} ({Title})", entity.Id, entity.Title);
        return MediaItemDto.FromEntity(entity);
    }
}
