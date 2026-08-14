using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Documento de conocimiento asociado a una knowledge base. Guarda solo la
/// metadata; el archivo reposa en el storage de objetos bajo
/// <see cref="StorageKey"/>. El chunking + embeddings los realiza el AI Service
/// (estado reflejado en <see cref="Status"/> / <see cref="ChunksCount"/>).
/// </summary>
public sealed class AgentDocument
{
    public Guid Id { get; set; }

    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Clave del objeto en el storage, ej. <c>agents/docs/{id}.md</c>.</summary>
    public string StorageKey { get; set; } = default!;

    public string FileName { get; set; } = default!;

    /// <summary>Content-Type del archivo, ej. <c>text/markdown</c>.</summary>
    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public AgentDocumentStatus Status { get; set; } = AgentDocumentStatus.Pendiente;

    public string? ErrorMessage { get; set; }

    /// <summary>Chunks indexados por el AI Service (null mientras no se procese).</summary>
    public int? ChunksCount { get; set; }

    /// <summary>auth.users.Id del autor. Null hasta integrar Identity.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public KnowledgeBase? KnowledgeBase { get; set; }
}