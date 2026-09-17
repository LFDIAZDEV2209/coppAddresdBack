using CoppAddresd.Application.Features.Agents;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Consulta de ejecuciones de agentes al AI Service (monitoreo admin) y el
/// descriptor de grafo del agente (visualización de flujos). El frontend
/// nunca llama al AI Service directamente: pasa por este proxy.
/// </summary>
public interface IAgentExecutionsQueryService
{
    Task<AgentExecutionsListDto> ListAsync(AgentExecutionQueryOptions options, CancellationToken ct = default);

    Task<AgentExecutionDetailDto?> GetAsync(string executionId, CancellationToken ct = default);

    /// <summary>Descriptor del grafo (nodos/aristas + config) o null si no está disponible.</summary>
    Task<AgentGraphDto?> GetGraphAsync(string agentTypeId, CancellationToken ct = default);
}
