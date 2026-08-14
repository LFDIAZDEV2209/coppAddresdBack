using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Knowledge base: agrupación de documentos de conocimiento. Scope <c>Global</c>
/// (usable por todos los agentes) o <c>Agent</c> (exclusivo de un tipo de agente).
/// </summary>
public sealed class KnowledgeBase
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public KnowledgeBaseScope Scope { get; set; }

    /// <summary>Obligatorio cuando <see cref="Scope"/> es <c>Agent</c>.</summary>
    public Guid? AgentTypeId { get; set; }

    public AgentStatus Status { get; set; } = AgentStatus.Activo;

    /// <summary>auth.users.Id del creador. Null hasta integrar Identity.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public AgentType? AgentType { get; set; }

    public ICollection<AgentDocument> Documents { get; set; } = [];
}