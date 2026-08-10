namespace CoppAddresd.Application.DTOs.Ai;

public enum SseEventType
{
    Start,
    Token,
    Node,
    Done,
    Error
}

public record SseEvent(SseEventType Type, string? Content = null, string? Node = null, string? ThreadId = null);
