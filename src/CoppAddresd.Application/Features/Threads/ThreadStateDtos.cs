namespace CoppAddresd.Application.Features.Threads;

/// <summary>
/// Mensaje visible del historial de un thread (contrato aditivo del AI
/// Service): <c>role</c> ("user" | "bot") + <c>text</c>, en el orden del thread.
/// </summary>
public record ThreadMessageResult(string Role, string Text);

/// <summary>
/// Resumen del historial de un thread devuelto por el AI Service (proxy de
/// lectura del backend). El front lo consume con contrato camelCase
/// (threadId, messageCount, lastMessage, messages).
/// Contrato aditivo: <see cref="Messages"/> viaja en orden cronológico y el AI
/// Service lo limita a los últimos 100 mensajes visibles; un ai-service
/// anterior no envía el campo y se degrada a lista vacía.
/// </summary>
public record ThreadStateResult(
    string ThreadId,
    int MessageCount,
    string? LastMessage,
    IReadOnlyList<ThreadMessageResult>? Messages = null);