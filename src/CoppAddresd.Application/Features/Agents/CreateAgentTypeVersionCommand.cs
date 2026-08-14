using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Crea una versión de configuración para un tipo de agente. Si el tipo aún
/// no tiene versión activa, esta queda activa automáticamente y se notifica
/// al AI Service para precompilar el runtime.
/// </summary>
public record CreateAgentTypeVersionCommand(
    Guid AgentTypeId,
    string Config,
    string? Notes)
    : IRequest<AgentTypeVersionDto>;

public sealed class CreateAgentTypeVersionCommandHandler(
    IAgentCatalogRepository repository,
    IAgentRuntimeSyncService runtimeSync,
    ILogger<CreateAgentTypeVersionCommandHandler> logger)
    : IRequestHandler<CreateAgentTypeVersionCommand, AgentTypeVersionDto>
{
    public async Task<AgentTypeVersionDto> Handle(CreateAgentTypeVersionCommand request, CancellationToken ct)
    {
        var agentType = await repository.GetAgentTypeAsync(request.AgentTypeId, ct)
            ?? throw new InvalidOperationException($"El tipo de agente {request.AgentTypeId} no existe.");

        var versionNumber = await repository.NextVersionNumberAsync(agentType.Id, ct);
        var isFirst = versionNumber == 1;

        var version = new AgentTypeVersion
        {
            Id = Guid.NewGuid(),
            AgentTypeId = agentType.Id,
            VersionNumber = versionNumber,
            IsActive = isFirst,
            Config = request.Config,
            Notes = Normalize(request.Notes),
            CreatedAt = DateTime.UtcNow,
        };

        // Primera versión: se inserta y se apunta ActiveVersionId en una sola
        // transacción — el orden importa por la FK agent_types.active_version_id.
        if (isFirst)
        {
            await repository.AddFirstVersionAndActivateAsync(agentType, version, ct);
        }
        else
        {
            await repository.AddVersionAsync(version, ct);
        }

        logger.LogInformation(
            "Versión {VersionNumber} creada para agente {AgentTypeId} (activa: {IsActive})",
            version.VersionNumber, version.AgentTypeId, version.IsActive);

        if (version.IsActive)
            await AgentRuntimeSyncer.TrySyncAsync(runtimeSync, agentType, version, logger, ct);

        return AgentTypeVersionDto.FromEntity(version);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}