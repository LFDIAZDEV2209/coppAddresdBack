namespace CoppAddresd.Domain.Enums;

/// <summary>Estado operativo de un tipo de agente o knowledge base.</summary>
public enum AgentStatus
{
    Borrador,
    Activo,
    Inactivo,
}

/// <summary>Alcance de una knowledge base: conocimiento global (todos los
/// agentes) o exclusivo de un tipo de agente.</summary>
public enum KnowledgeBaseScope
{
    Global,
    Agent,
}

/// <summary>Estado del procesamiento de un documento (chunking + embeddings).</summary>
public enum AgentDocumentStatus
{
    Pendiente,
    Procesando,
    Listo,
    Error,
}

/// <summary>Estado de una instancia de agente asignada a un paciente.</summary>
public enum AgentInstanceStatus
{
    Activo,
    Inactivo,
}