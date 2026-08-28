using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceSettings _settings;
    private readonly ILogger<AiServiceClient> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        // Campos opcionales sin valor no se envían: evita que el ai-service
        // reciba `agent: null` (rechazado con 422 por pydantic) y deja que use
        // sus defaults (ej: agent -> "base").
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AiServiceClient(
        HttpClient httpClient,
        IOptions<AiServiceSettings> settings,
        ILogger<AiServiceClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    private static object BuildChatPayload(ChatRequest request) => new
    {
        message = request.Message,
        // `agent` no admite null (schema del AI Service, default "base").
        agent = string.IsNullOrWhiteSpace(request.Agent) ? "base" : request.Agent,
        thread_id = request.ThreadId,
        agent_type_id = request.AgentTypeId,
        user_id = request.UserId,
    };

    /// <summary>
    /// Payload de generación de plan: type + contexto clínico consolidado +
    /// restricciones de seguridad. Se serializa con JsonOpts (snake_case), por
    /// lo que los DTOs de contexto se envían acorde al contrato del AI Service.
    /// </summary>
    private static object BuildGeneratePlanPayload(
        string type,
        ClinicalContextDto context,
        IReadOnlyList<RestrictionDto> restrictions) => new
    {
        type,
        clinical_context = context,
        restrictions,
    };

    /// <summary>
    /// Header de autenticación del canal interno backend → AI Service. El
    /// frontend jamás lo conoce; la clave vive solo en configuración
    /// (appsettings/variables de entorno, gitignoreado).
    /// </summary>
    private void AddInternalKeyHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_settings.InternalApiKey))
        {
            _logger.LogWarning(
                "AiService:InternalApiKey no configurada — el AI Service rechazará la llamada (401/503).");
            return;
        }
        request.Headers.TryAddWithoutValidation("X-Internal-Key", _settings.InternalApiKey);
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("Calling AI service chat endpoint");
        var payload = BuildChatPayload(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.ChatEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        AddInternalKeyHeader(httpRequest);

        // JsonOpts (snake_case + case-insensitive + WhenWritingNull): el
        // payload se serializa acorde al contrato del ai-service y la
        // respuesta (`answer`/`thread_id`/`execution_id`/`agent`) se
        // deserializa correctamente. Los errores se propagan como
        // AiServiceException para habilitar el re-sync del agente.
        using var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<ChatResponseJson>(JsonOpts, cancellationToken: ct);
        _logger.LogDebug("AI service responded: ThreadId={ThreadId}", result?.ThreadId);
        return new ChatResponse(result!.Reply, result.ThreadId, result.ExecutionId, result.Agent);
    }

    public async Task<AiPlanResult> GeneratePlanAsync(
        string type,
        ClinicalContextDto context,
        IReadOnlyList<RestrictionDto> restrictions,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating {Type} plan via AI service", type);
        var payload = BuildGeneratePlanPayload(type, context, restrictions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.PlanGenerateEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        AddInternalKeyHeader(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<GeneratePlanResponseJson>(JsonOpts, ct);
        if (result is null
            || result.Plan.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new AiServiceException(
                (int)response.StatusCode,
                "El AI Service no devolvió un plan en la respuesta.");
        }

        _logger.LogInformation("AI service returned {Type} plan", result.Type);
        return new AiPlanResult(result.Type, result.Plan);
    }

    public async IAsyncEnumerable<SseEvent> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in StreamRawInternalAsync(request, ct))
        {
            var evt = ParseSseEvent(chunk);
            if (evt is not null)
                yield return evt;
        }
    }

    public async IAsyncEnumerable<StreamChatChunk> StreamRawAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in StreamRawInternalAsync(request, ct))
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<StreamChatChunk> StreamRawInternalAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogDebug("Starting stream to AI service");
        var payload = BuildChatPayload(request);
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.StreamEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        AddInternalKeyHeader(httpRequest);

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, ct);

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            
            yield return new StreamChatChunk(line);
        }
        
        _logger.LogDebug("Stream completed");
    }

    public async Task<ProactiveMessageResult> ProactiveMessageAsync(
        Guid userId,
        string message,
        string agentTypeId = "base",
        CancellationToken ct = default)
    {
        _logger.LogDebug("Injecting proactive message for userId={UserId}", userId);

        // `thread_id` NO se envía: el AI Service resuelve el thread estable
        // `proactive-{userId}`. El payload respeta el contrato del endpoint
        // (user_id, agent_type_id, message) en snake_case.
        var payload = new
        {
            user_id = userId.ToString(),
            agent_type_id = string.IsNullOrWhiteSpace(agentTypeId) ? "base" : agentTypeId,
            message,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.ProactiveMessageEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        AddInternalKeyHeader(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<ProactiveMessageResponseJson>(JsonOpts, ct);
        _logger.LogDebug("AI service injected proactive message: ThreadId={ThreadId}", result?.ThreadId);
        return new ProactiveMessageResult(result!.ThreadId, result.MessageId);
    }

    private static async Task ThrowForResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new AiServiceException((int)response.StatusCode, detail);
    }

    public async Task<ThreadStateResult> GetThreadStateAsync(
        string threadId,
        string userId,
        CancellationToken ct = default)
    {
        // Proxy de lectura del historial de un thread (canal interno). El AI
        // Service aísla el thread por user_id (`{user_id}::{thread_id}`) y
        // exige X-Internal-Key — el frontend jamás lo conoce.
        var url = $"{_settings.ApiPrefix}/threads/{Uri.EscapeDataString(threadId)}/state?user_id={Uri.EscapeDataString(userId)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        AddInternalKeyHeader(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowForResponseAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<ThreadStateResponseJson>(JsonOpts, ct);
        if (result is null)
            return new ThreadStateResult(threadId, 0, null);

        return new ThreadStateResult(result.ThreadId, result.MessageCount, result.LastMessage);
    }

    private SseEvent? ParseSseEvent(StreamChatChunk chunk)
    {
        var line = chunk.RawData;
        
        if (line.StartsWith("event: start", StringComparison.Ordinal))
            return new SseEvent(SseEventType.Start);
        
        if (line.StartsWith("data: ", StringComparison.Ordinal))
        {
            var data = line["data: ".Length..];
            
            if (data == "{}")
                return new SseEvent(SseEventType.Start);
            
            try
            {
                if (data.Contains("\"thread_id\""))
                {
                    var done = ParseJson<DoneJson>(data);
                    return new SseEvent(SseEventType.Done, ThreadId: done?.ThreadId);
                }
                if (data.Contains("\"node\""))
                {
                    var node = ParseJson<NodeJson>(data);
                    return new SseEvent(SseEventType.Node, Node: node?.Node);
                }
                if (data.Contains("\"token\""))
                {
                    var msg = ParseJson<MessageJson>(data);
                    if (msg?.Type == "token")
                        return new SseEvent(SseEventType.Token, Content: msg.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing SSE data: {Data}", data);
            }
        }
        
        return null;
    }

    private T? ParseJson<T>(string data)
    {
        return JsonSerializer.Deserialize<T>(data, JsonOpts);
    }

    // El contrato del ai-service responde `answer`, `thread_id`,
    // `execution_id` y `agent` (snake_case), no `reply`/`threadId`
    // (camelCase Web defaults).
    private record ChatResponseJson(
        [property: JsonPropertyName("answer")] string Reply,
        [property: JsonPropertyName("thread_id")] string ThreadId,
        [property: JsonPropertyName("execution_id")] string? ExecutionId = null,
        [property: JsonPropertyName("agent")] string? Agent = null);
    private record DoneJson(string ThreadId);
    private record NodeJson(string Node);
    private record MessageJson(string Type, string? Content);

    // Contrato del ai-service para generación de planes: `type` + `plan`
    // (snake_case). El plan es JSON crudo (JsonElement) para no acoplarse al
    // shape de los DTOs de creación del módulo Wellness.
    private sealed record GeneratePlanResponseJson(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("plan")] JsonElement Plan);

    // Contrato del endpoint de inyección proactiva: `thread_id` + `message_id`
    // (snake_case).
    private sealed record ProactiveMessageResponseJson(
        [property: JsonPropertyName("thread_id")] string ThreadId,
        [property: JsonPropertyName("message_id")] string MessageId);

    // Contrato del AI Service para el estado de un thread: `thread_id`,
    // `message_count` y `last_message` (snake_case).
    private sealed record ThreadStateResponseJson(
        [property: JsonPropertyName("thread_id")] string ThreadId,
        [property: JsonPropertyName("message_count")] int MessageCount,
        [property: JsonPropertyName("last_message")] string? LastMessage);
}
