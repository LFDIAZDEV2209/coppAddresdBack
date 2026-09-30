using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Transición semántica de archivado (REQ-PCA-03, design D5): cualquier estado
/// → <see cref="MediaStatus.Archived"/>. El medio deja de aparecer en los
/// selectores de nuevas asignaciones pero conserva su reproducción en las
/// semanas/plantillas previamente programadas. Idempotente.
/// </summary>
public record ArchiveMediaItemCommand(Guid Id) : IRequest<MediaItemDto>;

public sealed class ArchiveMediaItemCommandHandler(
    IMediaItemRepository repository,
    ILogger<ArchiveMediaItemCommandHandler> logger
) : IRequestHandler<ArchiveMediaItemCommand, MediaItemDto>
{
    public async Task<MediaItemDto> Handle(ArchiveMediaItemCommand request, CancellationToken ct)
    {
        var entity =
            await repository.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Medio {request.Id} no encontrado.");

        if (entity.Status != MediaStatus.Archived)
        {
            entity.Status = MediaStatus.Archived;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            // El UPDATE queda registrado en audit.activity_logs (trigger + GUC actor).
            await repository.UpdateAsync(entity, ct);
            logger.LogInformation("Media archived: {Id} ({Title})", entity.Id, entity.Title);
        }

        return MediaItemDto.FromEntity(entity);
    }
}
