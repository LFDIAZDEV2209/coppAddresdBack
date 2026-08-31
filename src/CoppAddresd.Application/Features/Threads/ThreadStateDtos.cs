namespace CoppAddresd.Application.Features.Threads;

/// <summary>
/// Resumen del historial de un thread devuelto por el AI Service (proxy de
/// lectura del backend). El front lo consume con contrato camelCase
/// (threadId, messageCount, lastMessage).
/// </summary>
public record ThreadStateResult(string ThreadId, int MessageCount, string? LastMessage);