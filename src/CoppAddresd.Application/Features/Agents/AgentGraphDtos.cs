namespace CoppAddresd.Application.Features.Agents;

/// <summary>Nodo del grafo de un agente (visualización de flujos en vivo).</summary>
public record AgentGraphNodeDto(
    string Id,
    string Label,
    string Kind,
    string? Description,
    IReadOnlyDictionary<string, object>? Meta);

/// <summary>Arista entre nodos del grafo (flujo directo o condicional).</summary>
public record AgentGraphEdgeDto(string Source, string Target, string Kind, string? Label);

/// <summary>Configuración RAG efectiva del agente.</summary>
public record AgentGraphRagDto(bool Enabled, int KnowledgeBaseCount, int TopK);

/// <summary>Configuración de memoria de largo plazo del agente.</summary>
public record AgentGraphMemoryDto(bool Enabled, IReadOnlyList<string> Categories);

/// <summary>Configuración efectiva con la que corre el agente.</summary>
public record AgentGraphConfigDto(
    string? Provider,
    string? Model,
    double? Temperature,
    int? MaxTokens,
    IReadOnlyList<string> Tools,
    AgentGraphRagDto Rag,
    AgentGraphMemoryDto Memory,
    int MaxToolCalls,
    int RecursionLimit);

/// <summary>
/// Descriptor del grafo de un tipo de agente (proxy del AI Service): nodos,
/// aristas y configuración efectiva para el diagrama de flujos del playground.
/// </summary>
public record AgentGraphDto(
    string AgentTypeId,
    string Source,
    string? VersionId,
    IReadOnlyList<AgentGraphNodeDto> Nodes,
    IReadOnlyList<AgentGraphEdgeDto> Edges,
    AgentGraphConfigDto Config);
