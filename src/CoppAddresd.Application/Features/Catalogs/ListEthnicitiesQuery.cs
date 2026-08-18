using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista las etnias del catálogo demográfico (categorías OMB).</summary>
public record ListEthnicitiesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

public sealed class ListEthnicitiesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListEthnicitiesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListEthnicitiesQuery request, CancellationToken ct)
    {
        var items = await repository.ListEthnicitiesAsync(ct);
        return items.Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder)).ToList();
    }
}