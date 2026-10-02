namespace CoppAddresd.Application.Interfaces;

/// <summary>Resultado del procesamiento de un callback de estado de Twilio.</summary>
public enum SosWebhookOutcome
{
    /// <summary>Estado aplicado a la alerta.</summary>
    Processed,

    /// <summary>Firma inválida: el callback no proviene de Twilio.</summary>
    InvalidSignature,

    /// <summary>La alerta referida por el callback no existe.</summary>
    NotFound,

    /// <summary>Evento recibido sin cambios aplicables.</summary>
    Ignored,
}

/// <summary>
/// Procesa los webhooks de estado (<c>StatusCallback</c>) de Twilio para los
/// canales de voz y SMS del SOS. La verificación de firma es responsabilidad
/// de la implementación (Infrastructure usa <c>Twilio.Security.RequestValidator</c>).
/// </summary>
public interface ISosWebhookProcessor
{
    /// <summary>
    /// Aplica el estado de llamada reportado por Twilio (initiated/ringing/
    /// answered/completed + no-answer/busy/failed/canceled, duración).
    /// </summary>
    Task<SosWebhookOutcome> ProcessVoiceAsync(
        Guid? alertId,
        string url,
        string signature,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct = default
    );

    /// <summary>
    /// Aplica el estado de entrega del SMS reportado por Twilio
    /// (queued/sent/delivered/undelivered/failed).
    /// </summary>
    Task<SosWebhookOutcome> ProcessSmsAsync(
        Guid? alertId,
        string url,
        string signature,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct = default
    );
}
