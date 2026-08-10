using MediatR;

namespace CoppAddresd.Application.Features.Chat;

public record StreamChatCommand(string Message, string? Agent = null, string? ThreadId = null)
    : IRequest<IAsyncEnumerable<StreamChatChunk>>;

public record StreamChatChunk(string RawData);
