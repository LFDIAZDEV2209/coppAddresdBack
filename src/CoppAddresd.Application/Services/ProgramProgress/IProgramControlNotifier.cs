using CoppAddresd.Application.Features.Notifications;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Notificador de controles: abstracción de canal para el job
/// (<c>ProgramControlJob</c>). La implementación actual reutiliza el command
/// existente que hace fan-out FCM + inyección proactiva en el chat del paciente
/// (costo cero LLM); un canal futuro reemplaza esta interfaz sin tocar el
/// scheduler.
/// </summary>
public interface IProgramControlNotifier
{
    /// <summary>
    /// Envía el control al usuario: push a sus dispositivos + mensaje
    /// proactivo en su chat de IA. Devuelve el resumen del envío.
    /// </summary>
    Task<SendPushNotificationResult> NotifyAsync(
        Guid userId,
        string title,
        string message,
        string agentTypeId,
        CancellationToken ct = default);
}