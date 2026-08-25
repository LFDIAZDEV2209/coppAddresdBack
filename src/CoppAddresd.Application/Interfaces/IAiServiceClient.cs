using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Wellness;

namespace CoppAddresd.Application.Interfaces;

public interface IAiServiceClient
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<SseEvent> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<StreamChatChunk> StreamRawAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Solicita la generación de un plan (<c>nutrition</c> | <c>exercise</c>)
    /// condicionado por el contexto clínico consolidado y las restricciones de
    /// seguridad aplicables al paciente.
    /// </summary>
    Task<AiPlanResult> GeneratePlanAsync(
        string type,
        ClinicalContextDto context,
        IReadOnlyList<RestrictionDto> restrictions,
        CancellationToken ct = default);
}
