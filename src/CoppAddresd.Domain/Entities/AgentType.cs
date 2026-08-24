using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Catálogo de tipos de agente configurable (psicología, nutrición, medicina,
/// seguimiento...). La especialidad concreta no está hardcodeada: cada tipo
/// define su configuración vía versiones (<see cref="AgentTypeVersion"/>).
/// </summary>
public sealed class AgentType
{
    public Guid Id { get; set; }

    /// <summary>Nombre visible, ej. "Asistente de Nutrición".</summary>
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Especialidad/materia, ej. "nutrición".</summary>
    public string? Specialty { get; set; }

    /// <summary>Clave de icono para la UI (lucide).</summary>
    public string? IconKey { get; set; }

    /// <summary>Alias de routing (opcional, no único). Ej. "nutrition".</summary>
    public string? Slug { get; set; }

    public AgentStatus Status { get; set; } = AgentStatus.Borrador;

    /// <summary>Metadatos libres en JSON.</summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>Versión activa (la que usan los runtimes). Null si ninguna activa.</summary>
    public Guid? ActiveVersionId { get; set; }

    /// <summary>auth.users.Id del creador. Null hasta integrar Identity.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public AgentTypeVersion? ActiveVersion { get; set; }

    public ICollection<AgentTypeVersion> Versions { get; set; } = [];
}