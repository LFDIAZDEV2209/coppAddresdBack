using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

/// <summary>
/// Proxy del feedback hacia el ai-service (adaptive memory). Propaga el
/// <c>CancellationToken</c> de extremo a extremo y no filtra trazas internas:
/// los errores tipados (<c>AiServiceException</c>) los traduce el controller.
/// </summary>
public sealed class SendChatFeedbackCommandHandler(
    IAiServiceClient aiService,
    ILogger<SendChatFeedbackCommandHandler> logger
) : IRequestHandler<SendChatFeedbackCommand, ChatFeedbackResponseDto>
{
    public async Task<ChatFeedbackResponseDto> Handle(
        SendChatFeedbackCommand request,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            "Feedback de chat: ThreadId={ThreadId} Rating={Rating}",
            request.ThreadId,
            request.Rating
        );

        var dto = new ChatFeedbackRequestDto(
            request.ExecutionId,
            request.ThreadId,
            request.Rating,
            request.Comment
        );

        // UserId ya validado (no vacío) por FluentValidation; el contrato del
        // cliente lo exige no nulo.
        var result = await aiService.SendFeedbackAsync(dto, request.UserId!, ct);

        logger.LogInformation(
            "Feedback registrado: ThreadId={ThreadId} ExperienceSaved={ExperienceSaved}",
            result.ThreadId,
            result.ExperienceSaved
        );

        return result;
    }
}
