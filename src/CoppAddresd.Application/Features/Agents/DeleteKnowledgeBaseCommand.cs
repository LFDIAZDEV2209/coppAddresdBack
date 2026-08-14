using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Elimina una knowledge base. Se rechaza si tiene documentos (la FK RESTRICT
/// lo bloquearía; se valida antes para un error claro).
/// </summary>
public record DeleteKnowledgeBaseCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteKnowledgeBaseCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<DeleteKnowledgeBaseCommandHandler> logger) : IRequestHandler<DeleteKnowledgeBaseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteKnowledgeBaseCommand request, CancellationToken ct)
    {
        var entity = await repository.GetKnowledgeBaseAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"La knowledge base {request.Id} no existe.");

        var documents = await repository.ListDocumentsAsync(request.Id, ct);
        if (documents.Count > 0)
        {
            throw new InvalidOperationException(
                $"La knowledge base '{entity.Name}' tiene {documents.Count} documento(s) y no puede eliminarse.");
        }

        await repository.DeleteKnowledgeBaseAsync(entity, ct);

        logger.LogInformation("Knowledge base eliminada: {Id} ({Name})", entity.Id, entity.Name);

        return Unit.Value;
    }
}