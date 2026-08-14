using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Elimina el catálogo de un documento (el blob en storage se gestiona aparte).</summary>
public record DeleteAgentDocumentCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteAgentDocumentCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<DeleteAgentDocumentCommandHandler> logger) : IRequestHandler<DeleteAgentDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAgentDocumentCommand request, CancellationToken ct)
    {
        var entity = await repository.GetDocumentAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"El documento {request.Id} no existe.");

        await repository.DeleteDocumentAsync(entity, ct);

        logger.LogInformation("Documento eliminado: {Id} ({FileName})", entity.Id, entity.FileName);

        return Unit.Value;
    }
}