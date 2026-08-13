using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Elimina un medio existente. Devuelve <c>false</c> si no existe.</summary>
public record DeleteMediaItemCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteMediaItemCommandHandler(
    IMediaItemRepository repository,
    ILogger<DeleteMediaItemCommandHandler> logger) : IRequestHandler<DeleteMediaItemCommand, bool>
{
    public async Task<bool> Handle(DeleteMediaItemCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return false;

        await repository.DeleteAsync(entity, ct);

        logger.LogInformation("MediaItem deleted: {Id} ({Title})", entity.Id, entity.Title);
        return true;
    }
}
