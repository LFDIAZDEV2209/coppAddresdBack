using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Actualiza estado o metadatos de una instancia de agente.</summary>
public record UpdateAgentInstanceCommand(
    Guid Id,
    string? Status,
    string? Metadata)
    : IRequest<AgentInstanceDto>;

public sealed class UpdateAgentInstanceCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<UpdateAgentInstanceCommandHandler> logger) : IRequestHandler<UpdateAgentInstanceCommand, AgentInstanceDto>
{
    public async Task<AgentInstanceDto> Handle(UpdateAgentInstanceCommand request, CancellationToken ct)
    {
        var entity = await repository.GetInstanceAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"La instancia {request.Id} no existe.");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AgentInstanceStatus>(request.Status.Trim(), ignoreCase: true, out var status))
                throw new InvalidOperationException($"Estado de instancia inválido: '{request.Status}'.");

            entity.Status = status;
        }

        if (request.Metadata is not null)
            entity.Metadata = request.Metadata;

        entity.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateInstanceAsync(entity, ct);

        logger.LogInformation("Instancia de agente actualizada: {Id}", entity.Id);

        var updated = await repository.GetInstanceAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la instancia actualizada.");

        return AgentInstanceDto.FromEntity(updated);
    }
}