using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
        
        var response = await _httpClient.PostAsJsonAsync(_settings.ChatEndpoint, payload, ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ChatResponseJson>(cancellationToken: ct);
        _logger.LogDebug("AI service responded: ThreadId={ThreadId}", result?.ThreadId);
        return new ChatResponse(result!.Reply, result.ThreadId);
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

    private record ChatResponseJson(string Reply, string ThreadId);
    private record DoneJson(string ThreadId);
    private record NodeJson(string Node);
    private record MessageJson(string Type, string? Content);
}
