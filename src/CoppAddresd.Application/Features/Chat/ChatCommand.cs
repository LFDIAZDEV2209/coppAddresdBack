using MediatR;

namespace CoppAddresd.Application.Features.Chat;

public record ChatCommand(string Message, string? Agent = null, string? ThreadId = null)
    : IRequest<ChatResult>;

public record ChatResult(string Reply, string ThreadId);
