using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los tipos de documento del catálogo administrativo.</summary>
public record ListDocumentTypesQuery : IRequest<IReadOnlyList<CatalogOptionDto>>;

public sealed class ListDocumentTypesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListDocumentTypesQuery, IReadOnlyList<CatalogOptionDto>>
{
    public async Task<IReadOnlyList<CatalogOptionDto>> Handle(
        ListDocumentTypesQuery request, CancellationToken ct)
    {
        var items = await repository.ListDocumentTypesAsync(ct);
        return items.Select(x => new CatalogOptionDto(x.Id, x.Code, x.Name, x.SortOrder)).ToList();
    }
}