using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Elimina un tipo de agente. Se rechaza si tiene instancias asignadas
/// (la FK RESTRICT de la BD lo bloquearía; se valida antes para un error claro).
/// </summary>
public record DeleteAgentTypeCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteAgentTypeCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<DeleteAgentTypeCommandHandler> logger) : IRequestHandler<DeleteAgentTypeCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAgentTypeCommand request, CancellationToken ct)
    {
        var entity = await repository.GetAgentTypeAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"El tipo de agente {request.Id} no existe.");

        if (await repository.AgentTypeHasInstancesAsync(request.Id, ct))
        {
            throw new InvalidOperationException(
                $"El tipo de agente '{entity.Name}' tiene instancias asignadas y no puede eliminarse.");
        }

        await repository.DeleteAgentTypeAsync(entity, ct);

        logger.LogInformation("Tipo de agente eliminado: {Id} ({Name})", entity.Id, entity.Name);

        return Unit.Value;
    }
}