using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los estados de un país (ordenados por nombre).</summary>
public record ListStatesQuery(Guid CountryId) : IRequest<IReadOnlyList<StateDto>>;

/// <summary>
/// Cache-aside con TTL 1h, particionado por país
/// (clave: catalog:states:{countryId}:v1). Catálogo por seed, sin CRUD runtime.
/// </summary>
public sealed class ListStatesQueryHandler(ICatalogRepository repository, ICacheService cache)
    : IRequestHandler<ListStatesQuery, IReadOnlyList<StateDto>>
{
    public async Task<IReadOnlyList<StateDto>> Handle(ListStatesQuery request, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("states", request.CountryId),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var states = await repository.ListStatesByCountryAsync(request.CountryId, token);
                return states.Select(s => new StateDto(s.Id, s.Code, s.Name)).ToList();
            },
            ct
        );
    }
}
