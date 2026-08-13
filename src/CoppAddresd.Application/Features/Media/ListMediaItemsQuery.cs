using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Lista medios. Filtros opcionales por tipo, estado y publicado/disponible.
/// Ordenado por SortOrder (lecciones diarias) y luego por CreatedAt.
/// </summary>
public record ListMediaItemsQuery(
    MediaType? MediaType = null,
    MediaStatus? Status = null)
    : IRequest<IReadOnlyList<MediaItemDto>>;

public sealed class ListMediaItemsQueryHandler(
    IMediaItemRepository repository) : IRequestHandler<ListMediaItemsQuery, IReadOnlyList<MediaItemDto>>
{
    public async Task<IReadOnlyList<MediaItemDto>> Handle(ListMediaItemsQuery request, CancellationToken ct)
    {
        var items = await repository.ListAsync(ct);

        var filtered = items
            .Where(i => request.MediaType is null || i.MediaType == request.MediaType)
            .Where(i => request.Status is null || i.Status == request.Status)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt);

        return filtered.Select(MediaItemDto.FromEntity).ToList();
    }
}
