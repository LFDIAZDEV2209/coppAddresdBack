using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Busca medicamentos por nombre o NDC (autocomplete).</summary>
public record SearchMedicationsQuery(string? Search, int Limit = 30)
    : IRequest<IReadOnlyList<CatalogSearchItemDto>>;

public sealed class SearchMedicationsQueryHandler(ICatalogRepository repository)
    : IRequestHandler<SearchMedicationsQuery, IReadOnlyList<CatalogSearchItemDto>>
{
    public async Task<IReadOnlyList<CatalogSearchItemDto>> Handle(
        SearchMedicationsQuery request, CancellationToken ct)
    {
        var items = await repository.SearchMedicationsAsync(request.Search, request.Limit, ct);
        return items.Select(x => new CatalogSearchItemDto(x.Id, x.Ndc, x.Name, x.DrugClass)).ToList();
    }
}