using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Obtiene un medio por id. Devuelve <c>null</c> si no existe.</summary>
public record GetMediaItemQuery(Guid Id) : IRequest<MediaItemDto?>;

public sealed class GetMediaItemQueryHandler(
    IMediaItemRepository repository) : IRequestHandler<GetMediaItemQuery, MediaItemDto?>
{
    public async Task<MediaItemDto?> Handle(GetMediaItemQuery request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        return entity is null ? null : MediaItemDto.FromEntity(entity);
    }
}
