using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Elimina el catálogo de un documento y sus chunks del índice del AI Service.</summary>
public record DeleteAgentDocumentCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteAgentDocumentCommandHandler(
    IAgentCatalogRepository repository,
    IAgentRuntimeSyncService runtimeSync,
    ILogger<DeleteAgentDocumentCommandHandler> logger) : IRequestHandler<DeleteAgentDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAgentDocumentCommand request, CancellationToken ct)
    {
        var entity = await repository.GetDocumentAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"El documento {request.Id} no existe.");

        await repository.DeleteDocumentAsync(entity, ct);

        // Elimina los chunks del índice vectorial (best-effort: no falla el
        // borrado del catálogo si el AI Service no está disponible).
        await runtimeSync.DeleteDocumentChunksAsync(entity.Id, ct);

        logger.LogInformation("Documento eliminado: {Id} ({FileName})", entity.Id, entity.FileName);

        return Unit.Value;
    }
}