using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista las etnias del catálogo demográfico (categorías OMB).</summary>
public record ListEthnicitiesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

/// <summary>Cache-aside con TTL 1h (catálogo por seed). Clave: catalog:ethnicities:v1.</summary>
public sealed class ListEthnicitiesQueryHandler(ICatalogRepository repository, ICacheService cache)
    : IRequestHandler<ListEthnicitiesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListEthnicitiesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("ethnicities"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var items = await repository.ListEthnicitiesAsync(token);
                return items
                    .Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder))
                    .ToList();
            },
            ct
        );
    }
}
