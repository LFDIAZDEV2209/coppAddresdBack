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
    string? Slug,
    string Config);

/// <summary>Payload de ingestión de un documento al AI Service (blob en base64).</summary>
public record AgentDocumentIngestPayload(
    Guid DocumentId,
    Guid KnowledgeBaseId,
    string FileName,
    string ContentBase64);

/// <summary>Respuesta de la ingestión de un documento (status + conteo de chunks).</summary>
public record AgentDocumentIngestResult(
    string Status,
    int ChunksCreated,
    int ReplacedChunks,
    string? Error);

/// <summary>
/// Notifica al AI Service los cambios del catálogo de agentes (versión activa,
/// documentos indexados). Permite al runtime precompilar/cachear grafos y
/// mantener el índice vectorial sincronizado sin round-trips en cada petición.
/// </summary>
public interface IAgentRuntimeSyncService
{
    /// <summary>Envía la configuración del agente al endpoint interno del AI Service.</summary>
    Task SyncAgentConfigAsync(AgentRuntimeConfigPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Indexa un documento en el AI Service (chunks + embeddings en pgvector).
    /// </summary>
    Task<AgentDocumentIngestResult> IngestDocumentAsync(
        AgentDocumentIngestPayload payload,
        CancellationToken ct = default);

    /// <summary>Elimina los chunks de un documento del índice vectorial.</summary>
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
}