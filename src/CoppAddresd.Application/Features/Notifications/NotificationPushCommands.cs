using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Notifications;

/// <summary>
/// Envía una notificación push (FCM) a todos los dispositivos del usuario y
/// coordina la inyección del mensaje en su chat (best-effort, sin LLM). El
/// disparador es el ERP (admin); el userId destino viene en el body.
/// </summary>
public record SendPushNotificationCommand(
    Guid UserId,
    string Title,
    string Body,
    string? AgentTypeId = null,
    bool SkipPush = false,
    bool SkipChat = false,
    IReadOnlyDictionary<string,string>? Data = null,
    string? EnglishTitle = null,
    string? EnglishBody = null,
    string? ChatBody = null
) : IRequest<SendPushNotificationResult>;

/// <summary>Resumen del envío de la notificación push.</summary>
public record SendPushNotificationResult(
    int SentCount,
    int FailedCount,
    string? ThreadId,
    int TokenCount = 0,
    int DisabledCount = 0
);

/// <summary>Payload de POST /api/v1/notifications/send.</summary>
public record SendPushNotificationRequest(
    Guid UserId,
    string Title,
    string Body,
    string? AgentTypeId = null,
    string? EnglishTitle = null,
    string? EnglishBody = null
);

public sealed class SendPushNotificationCommandHandler(
    IDeviceTokenRepository repository,
    IFcmClient fcmClient,
    IAiServiceClient aiService,
    ILogger<SendPushNotificationCommandHandler> logger,
    IContentTranslationService? translations = null
) : IRequestHandler<SendPushNotificationCommand, SendPushNotificationResult>
{
    public async Task<SendPushNotificationResult> Handle(
        SendPushNotificationCommand request,
        CancellationToken ct
    )
    {
        var tokens = request.SkipPush ? [] : await repository.GetByUserIdAsync(request.UserId, ct);
        if (tokens.Count == 0)
        {
            logger.LogInformation(
                "Notificación push: sin dispositivos registrados para userId={UserId}.",
                request.UserId
            );
        }

        // Metadatos del push: el cliente mapea el thread de chat donde se
        // inyectó el mensaje del bot.
        var data = new Dictionary<string, string>
        {
            ["thread_id"] = $"proactive-{request.UserId}",
            ["type"] = "chat",
        };

        if (request.Data is not null) foreach (var item in request.Data) data[item.Key]=item.Value;

        var agentTypeId = string.IsNullOrWhiteSpace(request.AgentTypeId)
            ? "base"
            : request.AgentTypeId;

        var sent = 0;
        var failed = 0;
        var disabled = 0;

        // Resuelve todos los idiomas necesarios antes del primer envío para evitar entregas parciales si falla la traducción.
        var copy = await PushContentLocalization.PrepareAsync(tokens, request.Title, request.Body, translations, ct,
            request.EnglishTitle, request.EnglishBody);

        foreach (var token in tokens)
        {
            var result = await fcmClient.SendAsync(
                token.Token,
                copy[PushContentLocalization.Language(token)].Title,
                copy[PushContentLocalization.Language(token)].Body,
                data,
                ct
            );

            switch (result.Status)
            {
                case FcmSendStatus.Sent:
                    sent++;
                    break;

                case FcmSendStatus.Disabled:
                    // FCM no configurado: no cuenta como error del dispositivo.
                    disabled++;
                    logger.LogDebug(
                        "Push degradado (FCM deshabilitado): tokenId={TokenId}.",
                        token.Id
                    );
                    break;

                case FcmSendStatus.TokenInvalid:
                    // Token obsoleto: se elimina para no reintentarlo en el
                    // próximo envío (tarea futura: log notification_deliveries).
                    failed++;
                    var removed = await repository.DeleteByTokenAsync(token.Token, ct);
                    logger.LogInformation(
                        "Token FCM obsoleto eliminado (removed={Removed}): tokenId={TokenId}.",
                        removed,
                        token.Id
                    );
                    break;

                default:
                    failed++;
                    logger.LogWarning(
                        "Push FCM falló: tokenId={TokenId}, error={ErrorCode}.",
                        token.Id,
                        result.ErrorCode
                    );
                    break;
            }
        }

        // Inyección en el chat: best-effort — el push es lo principal. Si el
        // ai-service está caído o rechaza, se loguea y se continúa.
        string? threadId = null;
        try
        {
            if (!request.SkipChat)
            {
            var proactive = await aiService.ProactiveMessageAsync(
                request.UserId,
                request.ChatBody ?? request.Body,
                agentTypeId,
                "bot",
                ct
            );
            threadId = proactive.ThreadId;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Inyección proactiva falló (best-effort) para userId={UserId}.",
                request.UserId
            );
        }

        logger.LogInformation(
            "Notificación push completada: userId={UserId}, sent={Sent}, failed={Failed}, disabled={Disabled}, tokens={TokenCount}.",
            request.UserId,
            sent,
            failed,
            disabled,
            tokens.Count
        );

        return new SendPushNotificationResult(sent, failed, threadId, tokens.Count, disabled);
    }
}
