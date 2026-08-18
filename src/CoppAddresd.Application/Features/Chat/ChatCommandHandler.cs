using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

public class ChatCommandHandler : IRequestHandler<ChatCommand, ChatResult>
{
    private readonly IAiServiceClient _aiService;
    private readonly IAgentCatalogRepository _repository;
    private readonly IAgentRuntimeSyncService _runtimeSync;
    private readonly ILogger<ChatCommandHandler> _logger;

    public ChatCommandHandler(
        IAiServiceClient aiService,
        IAgentCatalogRepository repository,
        IAgentRuntimeSyncService runtimeSync,
        ILogger<ChatCommandHandler> logger)
    {
        _aiService = aiService;
        _repository = repository;
        _runtimeSync = runtimeSync;
        _logger = logger;
    }

    public async Task<ChatResult> Handle(ChatCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Chat request: Agent={Agent}, ThreadId={ThreadId}", request.Agent, request.ThreadId);

        var dto = new DTOs.Ai.ChatRequest(
            request.Message,
            request.Agent,
            request.ThreadId,
            request.AgentTypeId,
            request.UserId);

        try
        {
            var result = await _aiService.ChatAsync(dto, ct);
            _logger.LogInformation("Chat response: ThreadId={ThreadId}", result.ThreadId);
            return new ChatResult(result.Reply, result.ThreadId, result.ExecutionId);
        }
        catch (AiServiceException exc) when (exc.StatusCode == 404 && request.AgentTypeId is not null)
        {
            // El agente no está sincronizado en el runtime del AI Service
            // (p. ej. el sync de activación falló mientras estaba caído):
            // se re-sincroniza la versión activa y se reintenta una vez.
            _logger.LogWarning(
                "Agente {AgentTypeId} no sincronizado en el runtime; re-sincronizando...",
                request.AgentTypeId);

            var resynced = await AgentRuntimeReconciler.TryResyncAsync(
                _repository, _runtimeSync, Guid.Parse(request.AgentTypeId), _logger, ct);
            if (!resynced)
                throw;

            var result = await _aiService.ChatAsync(dto, ct);
            _logger.LogInformation("Chat response (tras re-sync): ThreadId={ThreadId}", result.ThreadId);
            return new ChatResult(result.Reply, result.ThreadId, result.ExecutionId);
        }
    }
}
