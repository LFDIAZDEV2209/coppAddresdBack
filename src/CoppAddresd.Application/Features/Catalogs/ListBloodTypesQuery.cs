using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los grupos sanguíneos del catálogo clínico.</summary>
public record ListBloodTypesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

public sealed class ListBloodTypesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListBloodTypesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListBloodTypesQuery request, CancellationToken ct)
    {
        var items = await repository.ListBloodTypesAsync(ct);
        return items.Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder)).ToList();
    }
}