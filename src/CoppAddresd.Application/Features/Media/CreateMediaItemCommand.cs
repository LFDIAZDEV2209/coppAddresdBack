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
    MediaType MediaType,
    string StorageKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
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
            existing.MediaType = request.MediaType;
            existing.ContentType = request.ContentType;
            existing.FileSizeBytes = request.FileSizeBytes;
            existing.DurationSecs = request.DurationSecs;
            existing.SortOrder = request.SortOrder;
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
            MediaType = request.MediaType,
            StorageKey = request.StorageKey,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            DurationSecs = request.DurationSecs,
            Status = request.Status,
            SortOrder = request.SortOrder,
            PublishedAt = request.Status == MediaStatus.Published ? now : null,
            CreatedAt = now,
            CreatedBy = request.CreatedBy,
        };

        await repository.AddAsync(entity, ct);

        logger.LogInformation("MediaItem created: {Id} ({Title})", entity.Id, entity.Title);
        return MediaItemDto.FromEntity(entity);
    }
}
