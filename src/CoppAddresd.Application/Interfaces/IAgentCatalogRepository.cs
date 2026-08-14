using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del catálogo de agentes (tipos, versiones, knowledge bases,
/// documentos e instancias). La implementación EF vive en Infrastructure.
/// </summary>
public interface IAgentCatalogRepository
{
    // --- Tipos ---
    Task<AgentType?> GetAgentTypeAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentType>> ListAgentTypesAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<int> CountAgentTypesAsync(string? search, CancellationToken ct = default);
    Task<AgentType> AddAgentTypeAsync(AgentType agentType, CancellationToken ct = default);
    Task UpdateAgentTypeAsync(AgentType agentType, CancellationToken ct = default);
    Task DeleteAgentTypeAsync(AgentType agentType, CancellationToken ct = default);
    Task<bool> AgentTypeExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AgentTypeHasInstancesAsync(Guid id, CancellationToken ct = default);
    Task<bool> AgentTypeNameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default);

    // --- Versiones ---
    Task<AgentTypeVersion?> GetVersionAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentTypeVersion>> ListVersionsAsync(Guid agentTypeId, CancellationToken ct = default);
    Task<int> NextVersionNumberAsync(Guid agentTypeId, CancellationToken ct = default);
    Task<AgentTypeVersion> AddVersionAsync(AgentTypeVersion version, CancellationToken ct = default);
    Task UpdateVersionAsync(AgentTypeVersion version, CancellationToken ct = default);
    Task<AgentTypeVersion?> GetActiveVersionAsync(Guid agentTypeId, CancellationToken ct = default);

    // --- Knowledge bases ---
    Task<KnowledgeBase?> GetKnowledgeBaseAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeBase>> ListKnowledgeBasesAsync(Guid? agentTypeId, CancellationToken ct = default);
    Task<KnowledgeBase> AddKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default);
    Task UpdateKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default);
    Task DeleteKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default);

    // --- Documentos ---
    Task<AgentDocument?> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentDocument>> ListDocumentsAsync(Guid knowledgeBaseId, CancellationToken ct = default);
    Task<AgentDocument> AddDocumentAsync(AgentDocument document, CancellationToken ct = default);
    Task UpdateDocumentAsync(AgentDocument document, CancellationToken ct = default);
    Task DeleteDocumentAsync(AgentDocument document, CancellationToken ct = default);

    // --- Instancias ---
    Task<AgentInstance?> GetInstanceAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentInstance>> ListInstancesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentInstance>> ListInstancesByAgentTypeAsync(Guid agentTypeId, CancellationToken ct = default);
    Task<AgentInstance> AddInstanceAsync(AgentInstance instance, CancellationToken ct = default);
    Task UpdateInstanceAsync(AgentInstance instance, CancellationToken ct = default);
    Task DeleteInstanceAsync(AgentInstance instance, CancellationToken ct = default);
    Task<bool> InstanceExistsForUserAndTypeAsync(Guid userId, Guid agentTypeId, Guid? excludeId, CancellationToken ct = default);
}