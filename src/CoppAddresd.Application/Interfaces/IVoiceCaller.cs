namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resultado de una llamada de voz (canal de emergencia SOS).
/// </summary>
/// <param name="Success">Indica si el proveedor aceptó la llamada.</param>
/// <param name="ProviderCallId">Identificador de la llamada devuelto por el proveedor.</param>
/// <param name="Error">Detalle del fallo (null si fue exitosa).</param>
public sealed record VoiceCallResult(bool Success, string? ProviderCallId, string? Error);

/// <summary>
/// Abstracción de llamadas de voz (TTS) para el canal de emergencia SOS. La
/// implementación real (<c>TwilioVoiceCaller</c>) usa Twilio Calls con TwiML
/// <c>&lt;Say&gt;</c>; <c>NoOpVoiceCaller</c> deja traza en log para desarrollo.
/// El contrato es fail-soft: un fallo del proveedor se traduce a
/// <see cref="VoiceCallResult"/> con error, nunca a una excepción no
/// controlada en el despachador.
/// </summary>
public interface IVoiceCaller
{
    /// <summary>Nombre del proveedor activo (ej. <c>noop</c>, <c>twilio</c>).</summary>
    string Provider { get; }

    /// <summary>Indica si el canal está configurado para llamar realmente.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Coloca una llamada al número indicado y reproduce el guion TTS
    /// (<paramref name="sayText"/>) en el idioma indicado (ej. <c>es-US</c>).
    /// </summary>
    Task<VoiceCallResult> CallAsync(
        string phoneNumber,
        string sayText,
        string language,
        CancellationToken ct = default,
        string? statusCallbackUrl = null
    );
}
