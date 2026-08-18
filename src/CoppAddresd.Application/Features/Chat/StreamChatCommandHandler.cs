using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

public class StreamChatCommandHandler : IRequestHandler<StreamChatCommand, IAsyncEnumerable<StreamChatChunk>>
{
    private readonly IAiServiceClient _aiService;
    private readonly IAgentCatalogRepository _repository;
    private readonly IAgentRuntimeSyncService _runtimeSync;
    private readonly ILogger<StreamChatCommandHandler> _logger;

    public StreamChatCommandHandler(
        IAiServiceClient aiService,
        IAgentCatalogRepository repository,
        IAgentRuntimeSyncService runtimeSync,
        ILogger<StreamChatCommandHandler> logger)
    {
        _aiService = aiService;
        _repository = repository;
        _runtimeSync = runtimeSync;
        _logger = logger;
    }

    public Task<IAsyncEnumerable<StreamChatChunk>> Handle(StreamChatCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Stream chat request: Agent={Agent}, ThreadId={ThreadId}", request.Agent, request.ThreadId);

        var dto = new DTOs.Ai.ChatRequest(
            request.Message,
            request.Agent,
            request.ThreadId,
            request.AgentTypeId,
            request.UserId);

        // El stream es lazy: el 404 del runtime surge en el primer MoveNext
        // (el SendAsync). Se envuelve el enumerador para re-sincronizar el
        // agente y reintentar una vez antes de fallar.
        return Task.FromResult(StreamWithResyncAsync(dto, request.AgentTypeId, ct));
    }

    private async IAsyncEnumerable<StreamChatChunk> StreamWithResyncAsync(
        DTOs.Ai.ChatRequest dto,
        string? agentTypeId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var resynced = false;
        while (true)
        {
            var enumerator = _aiService.StreamRawAsync(dto, ct).GetAsyncEnumerator(ct);
            StreamChatChunk first;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    await enumerator.DisposeAsync();
                    yield break;
                }
                first = enumerator.Current;
            }
            catch (AiServiceException exc) when (exc.StatusCode == 404 && agentTypeId is not null && !resynced)
            {
                await enumerator.DisposeAsync();
                _logger.LogWarning(
                    "Agente {AgentTypeId} no sincronizado en el runtime; re-sincronizando...",
                    agentTypeId);

                resynced = await AgentRuntimeReconciler.TryResyncAsync(
                    _repository, _runtimeSync, Guid.Parse(agentTypeId), _logger, ct);
                if (!resynced)
                    throw;
                continue;
            }

            try
            {
                yield return first;
                while (await enumerator.MoveNextAsync())
                {
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
            yield break;
        }
    }
}