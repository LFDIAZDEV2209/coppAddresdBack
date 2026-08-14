using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Asigna un agente a un paciente (una instancia por par user+type). El tipo
/// debe existir; su estado Activo se valida en la capa de negocio.
/// </summary>
public record AssignAgentInstanceCommand(
    Guid UserId,
    Guid AgentTypeId,
    string? Metadata)
    : IRequest<AgentInstanceDto>;

public sealed class AssignAgentInstanceCommandHandler(
    IAgentCatalogRepository repository,
    ILogger<AssignAgentInstanceCommandHandler> logger) : IRequestHandler<AssignAgentInstanceCommand, AgentInstanceDto>
{
    public async Task<AgentInstanceDto> Handle(AssignAgentInstanceCommand request, CancellationToken ct)
    {
        var agentType = await repository.GetAgentTypeAsync(request.AgentTypeId, ct)
            ?? throw new InvalidOperationException($"El tipo de agente {request.AgentTypeId} no existe.");

        if (agentType.Status != AgentStatus.Activo)
        {
            throw new InvalidOperationException(
                $"El tipo de agente '{agentType.Name}' no está activo y no puede asignarse.");
        }

        if (await repository.InstanceExistsForUserAndTypeAsync(request.UserId, request.AgentTypeId, null, ct))
        {
            throw new InvalidOperationException(
                $"El usuario {request.UserId} ya tiene el agente '{agentType.Name}' asignado.");
        }

        var entity = new AgentInstance
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            AgentTypeId = request.AgentTypeId,
            Status = AgentInstanceStatus.Activo,
            Metadata = string.IsNullOrWhiteSpace(request.Metadata) ? "{}" : request.Metadata,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddInstanceAsync(entity, ct);

        logger.LogInformation("Agente asignado: {AgentTypeId} → {UserId}",
            entity.AgentTypeId, entity.UserId);

        var created = await repository.GetInstanceAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la instancia creada.");

        return AgentInstanceDto.FromEntity(created);
    }
}