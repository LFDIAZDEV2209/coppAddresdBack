using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Helper compartido para notificar al AI Service la activación de una versión
/// de agente. La falla de notificación no revierte la activación en BD: el
/// runtime re-sincroniza bajo demanda en el primer chat.
/// </summary>
internal static class AgentRuntimeSyncer
{
    public static async Task TrySyncAsync(
        IAgentRuntimeSyncService runtimeSync,
        AgentType agentType,
        AgentTypeVersion version,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            await runtimeSync.SyncAgentConfigAsync(new AgentRuntimeConfigPayload(
                agentType.Id,
                version.Id,
                version.VersionNumber,
                agentType.Name,
                agentType.Description,
                agentType.Specialty,
                agentType.IconKey,
                agentType.Slug,
                version.Config), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo notificar al AI Service la activación de la versión {VersionId}",
                version.Id);
        }
    }
}