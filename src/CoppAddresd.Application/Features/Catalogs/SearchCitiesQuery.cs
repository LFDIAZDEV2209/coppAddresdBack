using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Busca ciudades de un estado por nombre (autocomplete, límite por defecto 30).</summary>
public record SearchCitiesQuery(Guid StateId, string? Search, int Limit = 30)
    : IRequest<IReadOnlyList<CityDto>>;

public sealed class SearchCitiesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<SearchCitiesQuery, IReadOnlyList<CityDto>>
{
    public async Task<IReadOnlyList<CityDto>> Handle(SearchCitiesQuery request, CancellationToken ct)
    {
        var cities = await repository.SearchCitiesAsync(request.StateId, request.Search, request.Limit, ct);
        return cities.Select(c => new CityDto(c.Id, c.Name)).ToList();
    }
}