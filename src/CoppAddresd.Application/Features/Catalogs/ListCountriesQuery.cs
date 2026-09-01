using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los países activos del catálogo (ordenados por SortOrder y nombre).</summary>
public record ListCountriesQuery : IRequest<IReadOnlyList<CountryDto>>;

/// <summary>Cache-aside con TTL 1h (catálogo por seed). Clave: catalog:countries:v1.</summary>
public sealed class ListCountriesQueryHandler(ICatalogRepository repository, ICacheService cache)
    : IRequestHandler<ListCountriesQuery, IReadOnlyList<CountryDto>>
{
    public async Task<IReadOnlyList<CountryDto>> Handle(
        ListCountriesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("countries"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var countries = await repository.ListCountriesAsync(token);
                return countries
                    .Select(c => new CountryDto(
                        c.Id,
                        c.Code,
                        c.Name,
                        c.PhoneCode,
                        c.IsActive,
                        c.SortOrder
                    ))
                    .ToList();
            },
            ct
        );
    }
}
