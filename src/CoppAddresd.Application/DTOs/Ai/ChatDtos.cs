namespace CoppAddresd.Application.DTOs.Ai;

public record ChatRequest(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null);

public record ChatResponse(string Reply, string ThreadId, string? ExecutionId = null, string? Agent = null);

/// <summary>
/// Resultado de la inyección proactiva de un mensaje del bot en el thread
/// estable del usuario (endpoint <c>/internal/agents/proactive-message</c>,
/// sin LLM).
/// </summary>
public record ProactiveMessageResult(string ThreadId, string MessageId);
