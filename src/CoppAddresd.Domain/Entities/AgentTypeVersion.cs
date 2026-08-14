namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Versión inmutable de la configuración de un tipo de agente. Permite saber
/// exactamente qué configuración usó una ejecución histórica y activar una
/// versión sin modificar las anteriores (no destructivo).
/// </summary>
public sealed class AgentTypeVersion
{
    public Guid Id { get; set; }

    public Guid AgentTypeId { get; set; }

    /// <summary>Número de versión correlativo dentro del tipo (1, 2, 3...).</summary>
    public int VersionNumber { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Configuración completa en JSON: system_prompt, prompt, provider, model,
    /// temperature, max_tokens, tools[], retrieval_config, memory_config,
    /// max_tool_calls, recursion_limit.
    /// </summary>
    public string Config { get; set; } = "{}";

    public string? Notes { get; set; }

    /// <summary>auth.users.Id del autor. Null hasta integrar Identity.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AgentType? AgentType { get; set; }
}