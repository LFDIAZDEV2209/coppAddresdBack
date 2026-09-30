using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

/// <summary>
/// Proxy de apertura de sesión de voz hacia el ai-service. Propaga el
/// <c>CancellationToken</c> de extremo a extremo y no filtra trazas internas:
/// los errores tipados (<c>AiServiceException</c>) los traduce el controller.
/// Auditoría estructurada SIN el signed URL (es la credencial temporal del
/// paciente) ni la API key.
/// </summary>
public sealed class VoiceSessionCommandHandler(
    IAiServiceClient aiService,
    ILogger<VoiceSessionCommandHandler> logger
) : IRequestHandler<VoiceSessionCommand, VoiceSessionResponseDto>
{
    public async Task<VoiceSessionResponseDto> Handle(
        VoiceSessionCommand request,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "Sesión de voz: UserId={UserId} PatientId={PatientId} ThreadId={ThreadId}",
            request.UserId,
            request.PatientId ?? "-",
            request.ThreadId ?? "-"
        );

        var result = await aiService.CreateVoiceSessionAsync(
            new VoiceSessionInternalRequest(request.UserId, request.PatientId, request.ThreadId),
            ct
        );

        logger.LogInformation(
            "Sesión de voz creada: UserId={UserId} AgentId={AgentId} ConversationId={ConversationId}",
            request.UserId,
            result.AgentId,
            result.ConversationId ?? "-"
        );

        return result;
    }
}
