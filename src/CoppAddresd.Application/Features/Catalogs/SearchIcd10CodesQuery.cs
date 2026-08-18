using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Busca códigos ICD-10 por código o descripción (autocomplete).</summary>
public record SearchIcd10CodesQuery(string? Search, int Limit = 30)
    : IRequest<IReadOnlyList<CatalogSearchItemDto>>;

public sealed class SearchIcd10CodesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<SearchIcd10CodesQuery, IReadOnlyList<CatalogSearchItemDto>>
{
    public async Task<IReadOnlyList<CatalogSearchItemDto>> Handle(
        SearchIcd10CodesQuery request, CancellationToken ct)
    {
        var items = await repository.SearchIcd10CodesAsync(request.Search, request.Limit, ct);
        return items.Select(x => new CatalogSearchItemDto(x.Id, x.Code, x.Description ?? x.Code, x.Description)).ToList();
    }
}