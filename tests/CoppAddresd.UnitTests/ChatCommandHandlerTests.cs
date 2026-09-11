using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del flujo de chat: mapeo de respuesta, propagación de errores del AI
/// Service, re-sync del runtime cuando el agente no está sincronizado y los
/// hooks de controles (fase 2): Responded + control_context y consumo de la
/// señal control_signal — siempre best-effort.
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
            ChatRequest request, Action<string>? onControlSignal = null, CancellationToken ct = default)
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

        public Task<CoppAddresd.Application.DTOs.LabExam.LabExamAiResponse> ExtractLabMetricsAsync(
            Guid patientId,
            Guid batchId,
            Stream fileStream,
            string fileName,
            string contentType,
            string? threadId = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<string> NarrateLabExamAsync(
            Guid patientId,
            Guid batchId,
            IReadOnlyList<CoppAddresd.Application.DTOs.LabExam.LabExamAiMetricDto> metrics,
            IReadOnlyDictionary<string, CoppAddresd.Application.DTOs.LabExam.MetricEvolution> previousMeasurements,
            string? language,
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

    /// <summary>
    /// Repositorio de controles fake: resolución configurable del control
    /// abierto y registro de las transiciones de la fase 2 (Responded y
    /// ClosedDeclined). El resto de los miembros no se usan en estos tests.
    /// </summary>
    private sealed class FakeProgramControlRepository : IProgramControlRepository
    {
        public ProgramControl? OpenControl { get; set; }

        public Func<Task<ProgramControl?>>? OnFindOpen { get; set; }

        public bool MarkRespondedResult { get; set; } = true;

        public int FindOpenCalls { get; private set; }

        public List<Guid> MarkRespondedCalls { get; } = [];

        public List<Guid> MarkClosedDeclinedCalls { get; } = [];

        public Task<ProgramControl?> FindOpenControlForUserAsync(
            Guid authUserId, string? threadId = null, CancellationToken ct = default)
        {
            FindOpenCalls++;
            return OnFindOpen?.Invoke() ?? Task.FromResult(OpenControl);
        }

        public Task<bool> MarkRespondedAsync(Guid id, DateTime respondedAt, CancellationToken ct = default)
        {
            MarkRespondedCalls.Add(id);
            return Task.FromResult(MarkRespondedResult);
        }

        public Task<bool> MarkClosedDeclinedAsync(Guid id, DateTime closedAt, CancellationToken ct = default)
        {
            MarkClosedDeclinedCalls.Add(id);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ProgramControlEnrollmentCandidate>> ListActiveCandidatesAsync(
            DateOnly startLocalDateCutoff, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ProgramControl?> GetAsync(
            Guid enrollmentId, int milestoneDay, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task AddAsync(ProgramControl control, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(ProgramControl control, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ProgramControlEnrollmentCandidate?> GetCandidateAsync(
            Guid enrollmentId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ProgramControl>> ListAsync(
            Guid? enrollmentId = null,
            Guid? patientId = null,
            ProgramControlStatus? status = null,
            int? limit = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<int> DeleteAsync(Guid? enrollmentId = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> MarkCompletedAsync(
            Guid id, Guid examBatchId, DateTime completedAt, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> MarkFollowedUpAsync(Guid id, DateTime followupSentAt, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> MarkMissedAsync(Guid id, DateTime missedAt, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> MarkNoUploadTimeoutAsync(Guid id, DateTime closedAt, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ProgramControlDueItem>> ListDueForFollowupAsync(
            DateTime utcNow, int followupHours, int limit = 100, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ProgramControlDueItem>> ListDueForMissAsync(
            DateTime utcNow, int missAfterFollowupHours, int limit = 100, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ProgramControlDueItem>> ListTimedOutNoUploadAsync(
            DateTime utcNow, int noUploadCloseHours, int limit = 100, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static ChatCommandHandler BuildHandler(
        IAiServiceClient aiClient,
        IAgentRuntimeSyncService? sync = null,
        IProgramControlRepository? programControls = null,
        bool controlsEnabled = true) => new(
            aiClient,
            new EmptyCatalogRepository(),
            sync ?? new FakeRuntimeSync(),
            programControls ?? new FakeProgramControlRepository(),
            Options.Create(new ProgramControlSettings { ControlsEnabled = controlsEnabled }),
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

    // -------------------------------------------------- Hooks de controles (fase 2)

    [Fact]
    public async Task Handle_control_abierto_en_Sent_marca_Responded_y_adjunta_contexto()
    {
        var userId = Guid.NewGuid().ToString();
        var control = new ProgramControl
        {
            Id = Guid.NewGuid(),
            MilestoneDay = 14,
            Status = ProgramControlStatus.Sent,
            ThreadId = "t-control",
        };
        var repo = new FakeProgramControlRepository { OpenControl = control };

        ChatRequest? captured = null;
        var ai = new FakeAiClient
        {
            OnChat = req =>
            {
                captured = req;
                return Task.FromResult(new ChatResponse("reply", "t1", "e1", "base"));
            },
        };
        var handler = BuildHandler(ai, programControls: repo);

        var result = await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-control", UserId: userId), CancellationToken.None);

        Assert.Equal("reply", result.Reply);
        var marked = Assert.Single(repo.MarkRespondedCalls);
        Assert.Equal(control.Id, marked);

        Assert.NotNull(captured!.ControlContext);
        var payload = Assert.IsType<ControlContextPayload>(captured.ControlContext);
        Assert.Equal(control.Id, payload.SendId);
        Assert.Equal(14, payload.MilestoneDay);
        Assert.Equal("responded", payload.Status); // tras marcar Responded
        Assert.True(payload.ExamPending);
    }

    [Fact]
    public async Task Handle_sin_control_abierto_no_marca_ni_adjunta_contexto()
    {
        var userId = Guid.NewGuid().ToString();
        var repo = new FakeProgramControlRepository(); // sin control abierto

        ChatRequest? captured = null;
        var ai = new FakeAiClient
        {
            OnChat = req =>
            {
                captured = req;
                return Task.FromResult(new ChatResponse("reply", "t1", "e1", "base"));
            },
        };
        var handler = BuildHandler(ai, programControls: repo);

        await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-x", UserId: userId), CancellationToken.None);

        Assert.Null(captured!.ControlContext);
        Assert.Empty(repo.MarkRespondedCalls);
    }

    [Fact]
    public async Task Handle_flag_off_no_consulta_controles()
    {
        var userId = Guid.NewGuid().ToString();
        var repo = new FakeProgramControlRepository
        {
            OpenControl = new ProgramControl { Id = Guid.NewGuid(), MilestoneDay = 7, Status = ProgramControlStatus.Sent },
        };
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse("reply", "t1", "e1", "base")),
        };
        var handler = BuildHandler(ai, programControls: repo, controlsEnabled: false);

        await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-x", UserId: userId), CancellationToken.None);

        Assert.Equal(0, repo.FindOpenCalls);
        Assert.Empty(repo.MarkRespondedCalls);
    }

    [Fact]
    public async Task Handle_signal_declined_cierra_control_y_no_expone_la_senal()
    {
        var userId = Guid.NewGuid().ToString();
        var control = new ProgramControl
        {
            Id = Guid.NewGuid(),
            MilestoneDay = 7,
            Status = ProgramControlStatus.Responded,
        };
        var repo = new FakeProgramControlRepository { OpenControl = control };
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse(
                "reply", "t1", "e1", "base", ControlSignal: "declined")),
        };
        var handler = BuildHandler(ai, programControls: repo);

        var result = await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-x", UserId: userId), CancellationToken.None);

        // La señal se consume (cierre por rechazo) pero jamás llega al ChatResult.
        var closed = Assert.Single(repo.MarkClosedDeclinedCalls);
        Assert.Equal(control.Id, closed);
        Assert.Equal("reply", result.Reply);
        Assert.Equal("t1", result.ThreadId);
    }

    [Fact]
    public async Task Handle_signal_otro_valor_no_cierra_el_control()
    {
        var userId = Guid.NewGuid().ToString();
        var repo = new FakeProgramControlRepository
        {
            OpenControl = new ProgramControl { Id = Guid.NewGuid(), MilestoneDay = 7, Status = ProgramControlStatus.Sent },
        };
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse(
                "reply", "t1", "e1", "base", ControlSignal: "otra_cosa")),
        };
        var handler = BuildHandler(ai, programControls: repo);

        await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-x", UserId: userId), CancellationToken.None);

        Assert.Empty(repo.MarkClosedDeclinedCalls);
    }

    [Fact]
    public async Task Handle_repo_thrower_no_rompe_el_chat()
    {
        var userId = Guid.NewGuid().ToString();
        var repo = new FakeProgramControlRepository
        {
            // La base de controles está caída: el chat debe seguir funcionando.
            OnFindOpen = () => throw new InvalidOperationException("db caída"),
        };
        var ai = new FakeAiClient
        {
            OnChat = _ => Task.FromResult(new ChatResponse("reply", "t1", "e1", "base")),
        };
        var handler = BuildHandler(ai, programControls: repo);

        var result = await handler.Handle(
            new ChatCommand("hola", ThreadId: "t-x", UserId: userId), CancellationToken.None);

        Assert.Equal("reply", result.Reply);
        Assert.Equal("t1", result.ThreadId);
    }
}
