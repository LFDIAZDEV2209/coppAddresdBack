namespace CoppAddresd.Application.DTOs.Ai;

/// <summary>
/// Request de chat hacia el ai-service. <c>ControlContext</c> es un campo
/// interno de la fase 2 de controles: cuando el usuario tiene un control de
/// programa abierto, viaja como <c>control_context</c> (snake_case) en el
/// payload; si es null el campo se OMITE (un ai-service anterior lo ignora y
/// el contrato queda igual a hoy).
/// </summary>
public record ChatRequest(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null,
    object? ControlContext = null);

/// <summary>
/// Contexto de control de programa abierto adjunto al request de chat (fase 2,
/// controles conversacionales): serializado como <c>control_context</c> con
/// <c>send_id</c> (id del control), <c>milestone_day</c>, <c>status</c> actual
/// (<c>sent</c>|<c>responded</c>|<c>followed_up</c>) y <c>exam_pending</c>
/// (siempre true: un control abierto implica examen no subido). El ai-service
/// lo usa para guiar la respuesta del agente sin exponerlo al paciente.
/// </summary>
public sealed record ControlContextPayload(
    Guid SendId,
    int MilestoneDay,
    string Status,
    bool ExamPending);

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
/// null cuando el ai-service no devuelve sugerencias. <c>ControlSignal</c> es
/// un canal interno de la fase 2 (valor <c>declined</c> cuando el paciente
/// rechazó subir el examen): el handler lo consume y NUNCA se expone en
/// <see cref="CoppAddresd.Application.Features.Chat.ChatResult"/>.
/// </summary>
public record ChatResponse(
    string Reply,
    string ThreadId,
    string? ExecutionId = null,
    string? Agent = null,
    IReadOnlyList<ChatSuggestion>? Suggestions = null,
    string? ControlSignal = null);

/// <summary>
/// Resultado de la inyección proactiva de un mensaje del bot en el thread
/// estable del usuario (endpoint <c>/internal/agents/proactive-message</c>,
/// sin LLM).
/// </summary>
public record ProactiveMessageResult(string ThreadId, string MessageId);
