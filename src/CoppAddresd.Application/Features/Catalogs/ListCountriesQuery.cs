using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los países activos del catálogo (ordenados por SortOrder y nombre).</summary>
public record ListCountriesQuery : IRequest<IReadOnlyList<CountryDto>>;

public sealed class ListCountriesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListCountriesQuery, IReadOnlyList<CountryDto>>
{
    public async Task<IReadOnlyList<CountryDto>> Handle(
        ListCountriesQuery request, CancellationToken ct)
    {
        var countries = await repository.ListCountriesAsync(ct);
        return countries
            .Select(c => new CountryDto(c.Id, c.Code, c.Name, c.PhoneCode, c.IsActive, c.SortOrder))
            .ToList();
    }
}