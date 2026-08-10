using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

public class StreamChatCommandHandler : IRequestHandler<StreamChatCommand, IAsyncEnumerable<StreamChatChunk>>
{
    private readonly IAiServiceClient _aiService;
    private readonly ILogger<StreamChatCommandHandler> _logger;

    public StreamChatCommandHandler(IAiServiceClient aiService, ILogger<StreamChatCommandHandler> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public Task<IAsyncEnumerable<StreamChatChunk>> Handle(StreamChatCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Stream chat request: Agent={Agent}, ThreadId={ThreadId}", request.Agent, request.ThreadId);
        
        var dto = new DTOs.Ai.ChatRequest(request.Message, request.Agent, request.ThreadId);
        return Task.FromResult(_aiService.StreamRawAsync(dto, ct));
    }
}
