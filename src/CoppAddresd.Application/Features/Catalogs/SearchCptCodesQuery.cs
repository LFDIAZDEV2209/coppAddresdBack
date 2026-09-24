using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Busca códigos CPT por código o descripción (autocomplete).</summary>
public record SearchCptCodesQuery(string? Search, int Limit = 30)
    : IRequest<IReadOnlyList<CatalogSearchItemDto>>;

public sealed class SearchCptCodesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<SearchCptCodesQuery, IReadOnlyList<CatalogSearchItemDto>>
{
    public async Task<IReadOnlyList<CatalogSearchItemDto>> Handle(
        SearchCptCodesQuery request, CancellationToken ct)
    {
        var items = await repository.SearchCptCodesAsync(request.Search, request.Limit, ct);
        return items.Select(x => new CatalogSearchItemDto(x.Id, x.Code, x.Description ?? x.Code, x.Description)).ToList();
    }
}
