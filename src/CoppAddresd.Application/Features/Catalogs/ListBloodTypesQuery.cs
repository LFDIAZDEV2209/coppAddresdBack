using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los grupos sanguíneos del catálogo clínico.</summary>
public record ListBloodTypesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

/// <summary>
/// Cache-aside con TTL 1h: catálogo de lectura masiva y escritura solo por
/// seed (sin CRUD runtime, no requiere invalidación). Clave: catalog:blood-types:v1.
/// </summary>
public sealed class ListBloodTypesQueryHandler(ICatalogRepository repository, ICacheService cache)
    : IRequestHandler<ListBloodTypesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListBloodTypesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("blood-types"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var items = await repository.ListBloodTypesAsync(token);
                return items
                    .Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder))
                    .ToList();
            },
            ct
        );
    }
}
