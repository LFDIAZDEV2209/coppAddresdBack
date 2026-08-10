using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;

namespace CoppAddresd.Application.Interfaces;

public interface IAiServiceClient
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<SseEvent> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<StreamChatChunk> StreamRawAsync(ChatRequest request, CancellationToken ct = default);
}
