namespace CoppAddresd.Application.DTOs.Ai;

public record ChatRequest(string Message, string? Agent = null, string? ThreadId = null);

public record ChatResponse(string Reply, string ThreadId);
