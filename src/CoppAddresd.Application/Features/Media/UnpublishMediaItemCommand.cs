using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Transición semántica de despublicación (REQ-PCA-03): Published → Draft.
/// Idempotente (un medio Draft se devuelve sin cambios). Un medio Archived
/// no se puede despublicar (→ 422): primero debe volver a borrador vía
/// edición explícita del ERP. Conserva <c>PublishedAt</c> como histórico.
/// </summary>
public record UnpublishMediaItemCommand(Guid Id) : IRequest<MediaItemDto>;

public sealed class UnpublishMediaItemCommandHandler(
    IMediaItemRepository repository,
    ILogger<UnpublishMediaItemCommandHandler> logger
) : IRequestHandler<UnpublishMediaItemCommand, MediaItemDto>
{
    public async Task<MediaItemDto> Handle(UnpublishMediaItemCommand request, CancellationToken ct)
    {
        var entity =
            await repository.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Medio {request.Id} no encontrado.");

        if (entity.Status == MediaStatus.Archived)
        {
            throw new UnprocessableEntityException(
                "No se puede despublicar un medio archivado; su estado debe gestionarse desde el editor del ERP."
            );
        }

        if (entity.Status != MediaStatus.Draft)
        {
            entity.Status = MediaStatus.Draft;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            // El UPDATE queda registrado en audit.activity_logs (trigger + GUC actor).
            await repository.UpdateAsync(entity, ct);
            logger.LogInformation("Media unpublished: {Id} ({Title})", entity.Id, entity.Title);
        }

        return MediaItemDto.FromEntity(entity);
    }
}
