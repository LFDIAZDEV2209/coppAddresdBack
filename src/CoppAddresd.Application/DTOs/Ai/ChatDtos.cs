namespace CoppAddresd.Application.DTOs.Ai;

public record ChatRequest(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null);

public record ChatResponse(string Reply, string ThreadId, string? ExecutionId = null);
