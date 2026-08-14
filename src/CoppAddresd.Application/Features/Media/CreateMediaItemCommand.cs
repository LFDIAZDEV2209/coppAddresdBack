using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Crea un nuevo medio multimedia desde el ERP.</summary>
public record CreateMediaItemCommand(
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
    Guid? CreatedBy = null)
    : IRequest<MediaItemDto>;

public sealed class CreateMediaItemCommandHandler(
    IMediaItemRepository repository,
    ILogger<CreateMediaItemCommandHandler> logger) : IRequestHandler<CreateMediaItemCommand, MediaItemDto>
{
    public async Task<MediaItemDto> Handle(CreateMediaItemCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Upsert por storageKey: si la subida ya se registró (retry), se actualiza
        // en vez de duplicar. Esto hace el POST idempotente.
        var existing = await repository.GetByStorageKeyAsync(request.StorageKey, ct);
        if (existing is not null)
        {
            existing.Title = request.Title;
            existing.Description = request.Description;
            existing.Author = request.Author;
            existing.MediaType = request.MediaType;
            existing.Category = request.Category;
            existing.ThumbnailKey = request.ThumbnailKey;
            existing.ContentType = request.ContentType;
            existing.FileSizeBytes = request.FileSizeBytes;
            existing.DurationSecs = request.DurationSecs;
            existing.SortOrder = request.SortOrder;
            existing.Day = request.Day;
            existing.Month = request.Month;
            if (request.Status == MediaStatus.Published && existing.PublishedAt is null)
                existing.PublishedAt = now;
            existing.Status = request.Status;
            existing.UpdatedAt = now;

            await repository.UpdateAsync(existing, ct);
            logger.LogInformation("MediaItem upserted: {Id} ({Title})", existing.Id, existing.Title);
            return MediaItemDto.FromEntity(existing);
        }

        var entity = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Author = request.Author,
            MediaType = request.MediaType,
            Category = request.Category,
            StorageKey = request.StorageKey,
            ThumbnailKey = request.ThumbnailKey,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            DurationSecs = request.DurationSecs,
            Status = request.Status,
            SortOrder = request.SortOrder,
            Day = request.Day,
            Month = request.Month,
            PublishedAt = request.Status == MediaStatus.Published ? now : null,
            CreatedAt = now,
            CreatedBy = request.CreatedBy,
        };

        await repository.AddAsync(entity, ct);

        logger.LogInformation("MediaItem created: {Id} ({Title})", entity.Id, entity.Title);
        return MediaItemDto.FromEntity(entity);
    }
}
