using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

public class ChatCommandHandler : IRequestHandler<ChatCommand, ChatResult>
{
    private readonly IAiServiceClient _aiService;
    private readonly ILogger<ChatCommandHandler> _logger;

    public ChatCommandHandler(IAiServiceClient aiService, ILogger<ChatCommandHandler> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<ChatResult> Handle(ChatCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Chat request: Agent={Agent}, ThreadId={ThreadId}", request.Agent, request.ThreadId);
        
        var dto = new DTOs.Ai.ChatRequest(request.Message, request.Agent, request.ThreadId);
        var result = await _aiService.ChatAsync(dto, ct);
        
        _logger.LogInformation("Chat response: ThreadId={ThreadId}", result.ThreadId);
        return new ChatResult(result.Reply, result.ThreadId, result.Agent);
    }
}
