namespace CoppAddresd.Application.DTOs.Ai;

public record ChatRequest(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null);

/// <summary>
/// Sugerencia de acción (CTA) que el ai-service adjunta a la respuesta de
/// chat, p. ej. agendar una cita. Hacia la app móvil se serializa como
/// camelCase: <c>type</c>, <c>ctaText</c>, <c>reason</c>, <c>urgency</c>.
/// </summary>
public sealed record ChatSuggestion(
    string Type,
    string CtaText,
    string? Reason = null,
    string Urgency = "normal");

/// <summary>
/// Respuesta de chat del ai-service. <c>Suggestions</c> son las acciones
/// sugeridas (CTA, p. ej. agendar cita) que el agente adjunta; puede ser
/// null cuando el ai-service no devuelve sugerencias.
/// </summary>
public record ChatResponse(
    string Reply,
    string ThreadId,
    string? ExecutionId = null,
    string? Agent = null,
    IReadOnlyList<ChatSuggestion>? Suggestions = null);

/// <summary>
/// Resultado de la inyección proactiva de un mensaje del bot en el thread
/// estable del usuario (endpoint <c>/internal/agents/proactive-message</c>,
/// sin LLM).
/// </summary>
public record ProactiveMessageResult(string ThreadId, string MessageId);
