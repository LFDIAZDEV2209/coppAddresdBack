using CoppAddresd.Application.Features.Agents;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Consulta de ejecuciones de agentes al AI Service (monitoreo admin).
/// El frontend nunca llama al AI Service directamente: pasa por este proxy.
/// </summary>
public interface IAgentExecutionsQueryService
{
    Task<AgentExecutionsListDto> ListAsync(AgentExecutionQueryOptions options, CancellationToken ct = default);

    Task<AgentExecutionDetailDto?> GetAsync(string executionId, CancellationToken ct = default);
}
