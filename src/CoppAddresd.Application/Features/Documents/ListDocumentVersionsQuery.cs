using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>Historial de versiones de un documento raíz (ordenado por versión desc).</summary>
public record ListDocumentVersionsQuery(Guid RootId) : IRequest<IReadOnlyList<DocumentVersionDto>>;

public sealed class ListDocumentVersionsQueryHandler(
    IDocumentRepository repository) : IRequestHandler<ListDocumentVersionsQuery, IReadOnlyList<DocumentVersionDto>>
{
    public async Task<IReadOnlyList<DocumentVersionDto>> Handle(
        ListDocumentVersionsQuery request,
        CancellationToken ct)
    {
        var versions = await repository.ListVersionsAsync(request.RootId, ct);
        return versions
            .OrderByDescending(v => v.Version)
            .Select(DocumentVersionDto.FromEntity)
            .ToList();
    }
}