using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.Chat;

public class StreamChatCommandHandler : IRequestHandler<StreamChatCommand, IAsyncEnumerable<StreamChatChunk>>
{
    private readonly IAiServiceClient _aiService;
    private readonly IAgentCatalogRepository _repository;
    private readonly IAgentRuntimeSyncService _runtimeSync;
    private readonly IProgramControlRepository _programControls;
    private readonly IOptions<ProgramControlSettings> _settings;
    private readonly ILogger<StreamChatCommandHandler> _logger;

    public StreamChatCommandHandler(
        IAiServiceClient aiService,
        IAgentCatalogRepository repository,
        IAgentRuntimeSyncService runtimeSync,
        IProgramControlRepository programControls,
        IOptions<ProgramControlSettings> settings,
        ILogger<StreamChatCommandHandler> logger)
    {
        _aiService = aiService;
        _repository = repository;
        _runtimeSync = runtimeSync;
        _programControls = programControls;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IAsyncEnumerable<StreamChatChunk>> Handle(StreamChatCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Stream chat request: Agent={Agent}, ThreadId={ThreadId}", request.Agent, request.ThreadId);

        // Hook de controles (fase 2, best-effort): control abierto → Responded
        // + control_context en el payload. Con el killswitch apagado no hay
        // ninguna llamada nueva.
        var openControl = await ProgramControlChatHooks.TryPrepareAsync(
            _programControls, _settings.Value, request.UserId, request.ThreadId, _logger, ct);

        var dto = new DTOs.Ai.ChatRequest(
            request.Message,
            request.Agent,
            request.ThreadId,
            request.AgentTypeId,
            request.UserId,
            openControl?.Payload);

        // El stream es lazy: el 404 del runtime surge en el primer MoveNext
        // (el SendAsync). Se envuelve el enumerador para re-sincronizar el
        // agente y reintentar una vez antes de fallar.
        return StreamWithResyncAsync(dto, request.AgentTypeId, openControl?.ControlId, ct);
    }

    private async IAsyncEnumerable<StreamChatChunk> StreamWithResyncAsync(
        DTOs.Ai.ChatRequest dto,
        string? agentTypeId,
        Guid? controlId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Señal de control capturada por el cliente (AiServiceClient consume
        // `event: control_signal` + `data:` y la entrega por callback — nunca
        // viaja al paciente).
        string? controlSignal = null;
        var resynced = false;
        while (true)
        {
            var enumerator = _aiService.StreamRawAsync(dto, signal => controlSignal = signal, ct)
                .GetAsyncEnumerator(ct);
            StreamChatChunk first;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    await enumerator.DisposeAsync();
                    break;
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
            break;
        }

        // Tras completar el stream se aplica la señal capturada (best-effort):
        // con "declined" el control se cierra por rechazo. La transición es
        // guardada — si el control ya avanzó (p. ej. a Responded) el claim
        // falla y se ignora.
        await ProgramControlChatHooks.TryConsumeSignalAsync(
            _programControls, controlSignal, controlId, _logger, ct);
    }
}