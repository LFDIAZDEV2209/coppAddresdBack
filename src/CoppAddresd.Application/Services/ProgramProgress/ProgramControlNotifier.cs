using CoppAddresd.Application.Features.Notifications;
using MediatR;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Notificador de controles basado en
/// <see cref="SendPushNotificationCommand"/>: reutiliza el camino existente
/// que (a) hace fan-out FCM a los dispositivos del usuario y (b) inyecta el
/// mensaje en su chat de IA como mensaje proactivo (best-effort, sin LLM).
/// El manejo de tokens obsoletos y degradación FCM ya vive en ese handler.
/// </summary>
public sealed class ProgramControlNotifier(IMediator mediator) : IProgramControlNotifier
{
    public async Task<SendPushNotificationResult> NotifyAsync(
        Guid userId,
        string title,
        string message,
        string agentTypeId,
        CancellationToken ct = default)
        => await mediator.Send(
            new SendPushNotificationCommand(userId, title, message, agentTypeId), ct);
}