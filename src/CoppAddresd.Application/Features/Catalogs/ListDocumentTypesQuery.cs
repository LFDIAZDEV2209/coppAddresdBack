using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los tipos de documento del catálogo administrativo.</summary>
public record ListDocumentTypesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

/// <summary>
/// Cache-aside con TTL 1h: catálogo gestionado por seed, sin CRUD runtime.
/// Clave: catalog:document-types:v1.
/// </summary>
public sealed class ListDocumentTypesQueryHandler(
    ICatalogRepository repository,
    ICacheService cache
) : IRequestHandler<ListDocumentTypesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListDocumentTypesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("document-types"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var items = await repository.ListDocumentTypesAsync(token);
                return items
                    .Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder))
                    .ToList();
            },
            ct
        );
    }
}
