using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Busca alergenos por nombre (autocomplete).</summary>
public record SearchAllergensQuery(string? Search, int Limit = 30)
    : IRequest<IReadOnlyList<CatalogSearchItemDto>>;

public sealed class SearchAllergensQueryHandler(ICatalogRepository repository)
    : IRequestHandler<SearchAllergensQuery, IReadOnlyList<CatalogSearchItemDto>>
{
    public async Task<IReadOnlyList<CatalogSearchItemDto>> Handle(
        SearchAllergensQuery request, CancellationToken ct)
    {
        var items = await repository.SearchAllergensAsync(request.Search, request.Limit, ct);
        return items.Select(x => new CatalogSearchItemDto(x.Id, null, x.Name, null)).ToList();
    }
}