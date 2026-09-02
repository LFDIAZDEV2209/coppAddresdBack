namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Abstracción para llamadas de voz (TTS) desde la capa de aplicación.
/// Implementaciones: <c>TwilioVoiceCaller</c> (producción) y
/// <c>LogVoiceCaller</c> (desarrollo, sin credenciales).
/// </summary>
public interface IVoiceCaller
{
    /// <summary>
    /// Coloca una llamada al número indicado y reproduce un mensaje
    /// sintetizado (TTS). Lanza en caso de fallo real.
    /// </summary>
    Task CallAsync(string to, string sayText, string language, CancellationToken ct = default);
}
