using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Instancia de agente asignada a un paciente (user de auth.users). Es la pieza
/// que la app móvil usa: cada paciente tiene su agente personalizado del tipo
/// indicado, con su memoria y conversaciones aisladas.
/// </summary>
public sealed class AgentInstance
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en auth.users (el paciente).</summary>
    public Guid UserId { get; set; }

    public Guid AgentTypeId { get; set; }

    public AgentInstanceStatus Status { get; set; } = AgentInstanceStatus.Activo;

    /// <summary>Metadatos libres en JSON (nombre personalizado, preferencias...).</summary>
    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public AgentType? AgentType { get; set; }
}