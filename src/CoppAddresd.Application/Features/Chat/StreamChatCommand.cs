using MediatR;

namespace CoppAddresd.Application.Features.Chat;

public record StreamChatCommand(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null,
    string? Language = null)
    : IRequest<IAsyncEnumerable<StreamChatChunk>>;

public record StreamChatChunk(string RawData);
