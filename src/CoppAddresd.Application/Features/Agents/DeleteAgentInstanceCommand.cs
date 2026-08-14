using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Desasigna un agente de un paciente.</summary>
public record DeleteAgentInstanceCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteAgentInstanceCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<DeleteAgentInstanceCommandHandler> logger) : IRequestHandler<DeleteAgentInstanceCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAgentInstanceCommand request, CancellationToken ct)
    {
        var entity = await repository.GetInstanceAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"La instancia {request.Id} no existe.");

        await repository.DeleteInstanceAsync(entity, ct);

        logger.LogInformation("Instancia de agente eliminada: {Id}", entity.Id);

        return Unit.Value;
    }
}