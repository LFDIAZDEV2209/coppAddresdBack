using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
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

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("Calling AI service chat endpoint");
        var payload = new { message = request.Message, agent = request.Agent, thread_id = request.ThreadId };
        
        // JsonOpts (snake_case + case-insensitive): el payload anónimo con
        // `thread_id` se serializa acorde al contrato del ai-service y la
        // respuesta (`answer`/`thread_id`) se deserializa correctamente.
        var response = await _httpClient.PostAsJsonAsync(_settings.ChatEndpoint, payload, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ChatResponseJson>(JsonOpts, cancellationToken: ct);
        _logger.LogDebug("AI service responded: ThreadId={ThreadId}", result?.ThreadId);
        return new ChatResponse(result!.Answer, result.ThreadId, result.Agent);
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
        var payload = new { message = request.Message, agent = request.Agent, thread_id = request.ThreadId };
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.StreamEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

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

    // El contrato del ai-service responde `answer`, `thread_id` y `agent`
    // (snake_case), no `reply`/`threadId` (camelCase Web defaults).
    private record ChatResponseJson(string Answer, string ThreadId, string? Agent);
    private record DoneJson(string ThreadId);
    private record NodeJson(string Node);
    private record MessageJson(string Type, string? Content);
}
