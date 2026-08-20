using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>Catálogo de documentos: categorías y tipos activos (para formularios).</summary>
public record ListDocumentCatalogQuery : IRequest<DocumentCatalogDto>;

public sealed class ListDocumentCatalogQueryHandler(
    IDocumentRepository repository) : IRequestHandler<ListDocumentCatalogQuery, DocumentCatalogDto>
{
    public async Task<DocumentCatalogDto> Handle(ListDocumentCatalogQuery request, CancellationToken ct)
    {
        var categories = await repository.ListCategoriesAsync(true, ct);
        var types = await repository.ListTypesAsync(categoryId: null, activeOnly: true, ct);

        return new DocumentCatalogDto(
            categories.OrderBy(c => c.SortOrder).Select(DocumentCategoryDto.FromEntity).ToList(),
            types.OrderBy(t => t.SortOrder).Select(ClinicalDocumentTypeDto.FromEntity).ToList());
    }
}