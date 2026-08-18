using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>
/// Reconciliación del runtime del AI Service: si un agente no está
/// sincronizado (p. ej. el sync de activación falló cuando el AI Service
/// estaba caído), el primer chat lo detecta (404 del runtime) y este helper
/// re-envía la versión activa antes de reintentar.
/// </summary>
internal static class AgentRuntimeReconciler
{
    /// <summary>
    /// Re-sincroniza el agente con el AI Service si tiene versión activa.
    /// Returns:
    ///     true si se encontró versión activa y el sync fue enviado;
    ///     false si el agente no existe o no tiene versión activa.
    /// </summary>
    public static async Task<bool> TryResyncAsync(
        IAgentCatalogRepository repository,
        IAgentRuntimeSyncService runtimeSync,
        Guid agentTypeId,
        ILogger logger,
        CancellationToken ct)
    {
        var agent = await repository.GetAgentTypeAsync(agentTypeId, ct);
        var version = agent?.ActiveVersion;
        if (agent is null || version is null)
        {
            logger.LogWarning(
                "No se pudo re-sincronizar el agente {AgentTypeId}: sin versión activa",
                agentTypeId);
            return false;
        }

        try
        {
            await runtimeSync.SyncAgentConfigAsync(new AgentRuntimeConfigPayload(
                agent.Id,
                version.Id,
                version.VersionNumber,
                agent.Name,
                agent.Description,
                agent.Specialty,
                agent.IconKey,
                version.Config), ct);
            logger.LogInformation(
                "Runtime re-sincronizado para el agente {AgentTypeId} (v{VersionNumber})",
                agentTypeId, version.VersionNumber);
            return true;
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Fallo el re-sync del runtime para {AgentTypeId}", agentTypeId);
            return false;
        }
    }
}
