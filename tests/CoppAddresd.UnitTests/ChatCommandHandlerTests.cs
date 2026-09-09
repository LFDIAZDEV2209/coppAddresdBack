using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del flujo de chat: mapeo de respuesta, propagación de errores del AI
/// Service y re-sync del runtime cuando el agente no está sincronizado.
/// </summary>
public class ChatCommandHandlerTests
{
    private sealed class FakeAiClient : IAiServiceClient
    {
        public Func<ChatRequest, Task<ChatResponse>>? OnChat { get; set; }

        public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
            => OnChat!(request);

        public IAsyncEnumerable<SseEvent> StreamChatAsync(
            ChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamChatChunk> StreamRawAsync(
            ChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AiPlanResult> GeneratePlanAsync(
            string type,
            ClinicalContextDto context,
            IReadOnlyList<RestrictionDto> restrictions,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ProactiveMessageResult> ProactiveMessageAsync(
            Guid userId,
            string message,
            string agentTypeId = "base",
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ThreadStateResult> GetThreadStateAsync(
            string threadId,
            string userId,
            CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeRuntimeSync : IAgentRuntimeSyncService
    {
        public List<AgentRuntimeConfigPayload> Synced { get; } = [];

        public Task SyncAgentConfigAsync(
            AgentRuntimeConfigPayload payload, CancellationToken ct = default)
        {
            Synced.Add(payload);
            return Task.CompletedTask;
        }

        public Task<AgentDocumentIngestResult> IngestDocumentAsync(
            AgentDocumentIngestPayload payload, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteDocumentChunksAsync(
            Guid documentId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>Repositorio fake: sin datos, GetAgentTypeAsync → null.</summary>
    private sealed class EmptyCatalogRepository : IAgentCatalogRepository
    {
        public Task<AgentType?> GetAgentTypeAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<AgentType?>(null);

        public Task<IReadOnlyList<AgentType>> ListAgentTypesAsync(
            int page, int pageSize, string? search, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<int> CountAgentTypesAsync(string? search, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentType> AddAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task SetActiveVersionAsync(
            Guid agentTypeId, Guid versionId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> AgentTypeExistsAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> AgentTypeHasInstancesAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> AgentTypeNameExistsAsync(
            string name, Guid? excludeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentTypeVersion?> GetVersionAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AgentTypeVersion>> ListVersionsAsync(
            Guid agentTypeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<int> NextVersionNumberAsync(Guid agentTypeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentTypeVersion> AddVersionAsync(
            AgentTypeVersion version, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentTypeVersion> AddFirstVersionAndActivateAsync(
            AgentType agentType, AgentTypeVersion version, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateVersionAsync(AgentTypeVersion version, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentTypeVersion?> GetActiveVersionAsync(
            Guid agentTypeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<KnowledgeBase?> GetKnowledgeBaseAsync(
            Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<KnowledgeBase>> ListKnowledgeBasesAsync(
            Guid? agentTypeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<KnowledgeBase> AddKnowledgeBaseAsync(
            KnowledgeBase kb, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentDocument?> GetDocumentAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AgentDocument>> ListDocumentsAsync(
            Guid knowledgeBaseId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentDocument> AddDocumentAsync(
            AgentDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateDocumentAsync(AgentDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteDocumentAsync(AgentDocument document, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentInstance?> GetInstanceAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AgentInstance>> ListInstancesAsync(
            Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AgentInstance>> ListInstancesByAgentTypeAsync(
            Guid agentTypeId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AgentInstance> AddInstanceAsync(
            AgentInstance instance, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateInstanceAsync(AgentInstance instance, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteInstanceAsync(AgentInstance instance, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> InstanceExistsForUserAndTypeAsync(
            Guid userId, Guid agentTypeId, Guid? excludeId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static ChatCommandHandler BuildHandler(
        IAiServiceClient aiClient,
        IAgentRuntimeSyncService? sync = null) => new(
            aiClient,
            new EmptyCatalogRepository(),
            sync ?? new FakeRuntimeSync(),
            NullLogger<ChatCommandHandler>.Instance);

    [Fact]
    public async Task Handle_exito_devuelve_ChatResult()
    {
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse("reply", "t1", "e1", "base")),
        };
        var handler = BuildHandler(ai);

        var result = await handler.Handle(
            new ChatCommand("hola", AgentTypeId: "guid-1", UserId: "user-1"), CancellationToken.None);

        Assert.Equal("reply", result.Reply);
        Assert.Equal("t1", result.ThreadId);
        Assert.Equal("e1", result.ExecutionId);
        Assert.Equal("base", result.Agent);
    }

    [Fact]
    public async Task Handle_passthrough_suggestions_del_AI()
    {
        var suggestions = new List<ChatSuggestion>
        {
            new("appointment", "Agenda tu cita aquí", "tu último examen sugiere un control", "normal"),
        };
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse("reply", "t1", "e1", "base", suggestions)),
        };
        var handler = BuildHandler(ai);

        var result = await handler.Handle(
            new ChatCommand("hola", AgentTypeId: "guid-1", UserId: "user-1"), CancellationToken.None);

        Assert.NotNull(result.Suggestions);
        var suggestion = Assert.Single(result.Suggestions!);
        Assert.Equal("appointment", suggestion.Type);
        Assert.Equal("Agenda tu cita aquí", suggestion.CtaText);
        Assert.Equal("tu último examen sugiere un control", suggestion.Reason);
        Assert.Equal("normal", suggestion.Urgency);
    }

    [Fact]
    public async Task Handle_sin_suggestions_devuelve_null_sin_romper()
    {
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse("reply", "t1", "e1", "base")),
        };
        var handler = BuildHandler(ai);

        var result = await handler.Handle(
            new ChatCommand("hola", AgentTypeId: "guid-1", UserId: "user-1"), CancellationToken.None);

        Assert.Null(result.Suggestions);
        Assert.Equal("reply", result.Reply);
        Assert.Equal("t1", result.ThreadId);
    }

    [Fact]
    public async Task Handle_404_sin_agentTypeId_propaga()
    {
        var ai = new FakeAiClient
        {
            OnChat = _ => throw new AiServiceException(404, "no encontrado"),
        };
        var handler = BuildHandler(ai);

        await Assert.ThrowsAsync<AiServiceException>(() =>
            handler.Handle(new ChatCommand("hola"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_404_sin_version_activa_reintenta_y_propaga()
    {
        var ai = new FakeAiClient
        {
            // El agente no está sincronizado y el re-sync no encuentra versión
            // activa en el catálogo → se reintenta una vez y se propaga.
            OnChat = _ => throw new AiServiceException(404, "no sincronizado"),
        };
        var sync = new FakeRuntimeSync();
        var handler = BuildHandler(ai, sync);

        await Assert.ThrowsAsync<AiServiceException>(() =>
            handler.Handle(
                new ChatCommand("hola", AgentTypeId: Guid.NewGuid().ToString()),
                CancellationToken.None));
    }
}
