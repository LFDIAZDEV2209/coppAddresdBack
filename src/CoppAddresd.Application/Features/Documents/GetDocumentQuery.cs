using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>Detalle de un documento (con conteo de versiones de su raíz). Devuelve <c>null</c> si no existe.</summary>
public record GetDocumentQuery(Guid Id) : IRequest<DocumentDto?>;

public sealed class GetDocumentQueryHandler(
    IDocumentRepository repository) : IRequestHandler<GetDocumentQuery, DocumentDto?>
{
    public async Task<DocumentDto?> Handle(GetDocumentQuery request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        var rootId = entity.ParentDocumentId ?? entity.Id;
        var versions = await repository.ListVersionsAsync(rootId, ct);

        return DocumentDto.FromEntity(entity, versions.Count);
    }
}