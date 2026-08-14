namespace CoppAddresd.Application.Interfaces;

/// <summary>Payload de sincronización de configuración de un agente al AI Service.</summary>
public record AgentRuntimeConfigPayload(
    Guid AgentTypeId,
    Guid VersionId,
    int VersionNumber,
    string Name,
    string? Description,
    string? Specialty,
    string? IconKey,
    string Config);

/// <summary>
/// Notifica al AI Service el cambio de versión activa de un tipo de agente.
/// Permite al runtime precompilar/cachear el grafo LangGraph sin round-trips
/// costosos en cada petición de chat.
/// </summary>
public interface IAgentRuntimeSyncService
{
    /// <summary>Envía la configuración del agente al endpoint interno del AI Service.</summary>
    Task SyncAgentConfigAsync(AgentRuntimeConfigPayload payload, CancellationToken ct = default);
}