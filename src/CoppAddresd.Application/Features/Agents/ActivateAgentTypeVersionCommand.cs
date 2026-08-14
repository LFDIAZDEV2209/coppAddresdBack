using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Activa una versión existente de un tipo de agente (desactiva el resto) y
/// notifica al AI Service para que precompile el runtime con la nueva versión.
/// </summary>
public record ActivateAgentTypeVersionCommand(Guid VersionId) : IRequest<AgentTypeVersionDto>;

public sealed class ActivateAgentTypeVersionCommandHandler(
    IAgentCatalogRepository repository,
    IAgentRuntimeSyncService runtimeSync,
    ILogger<ActivateAgentTypeVersionCommandHandler> logger)
    : IRequestHandler<ActivateAgentTypeVersionCommand, AgentTypeVersionDto>
{
    public async Task<AgentTypeVersionDto> Handle(ActivateAgentTypeVersionCommand request, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(request.VersionId, ct)
            ?? throw new InvalidOperationException($"La versión {request.VersionId} no existe.");

        if (version.IsActive)
            return AgentTypeVersionDto.FromEntity(version);

        var agentType = await repository.GetAgentTypeAsync(version.AgentTypeId, ct)
            ?? throw new InvalidOperationException($"El tipo de agente {version.AgentTypeId} no existe.");

        var versions = await repository.ListVersionsAsync(version.AgentTypeId, ct);

        foreach (var other in versions)
        {
            if (other.Id != version.Id && other.IsActive)
            {
                other.IsActive = false;
                await repository.UpdateVersionAsync(other, ct);
            }
        }

        version.IsActive = true;
        await repository.UpdateVersionAsync(version, ct);

        agentType.ActiveVersionId = version.Id;
        agentType.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAgentTypeAsync(agentType, ct);

        logger.LogInformation(
            "Versión {VersionId} activada para agente {AgentTypeId}",
            version.Id, version.AgentTypeId);

        await AgentRuntimeSyncer.TrySyncAsync(runtimeSync, agentType, version, logger, ct);

        return AgentTypeVersionDto.FromEntity(version);
    }
}