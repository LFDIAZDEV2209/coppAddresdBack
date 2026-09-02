namespace CoppAddresd.Application.Interfaces;

/// <summary>Mensaje SMS a enviar por el canal de emergencia SOS.</summary>
public sealed record SmsMessage(string To, string Body);

/// <summary>
/// Abstracción para envío de SMS desde la capa de aplicación.
/// Implementaciones: <c>TwilioSmsSender</c> (producción) y
/// <c>LogSmsSender</c> (desarrollo, sin credenciales).
/// </summary>
public interface ISmsSender
{
    /// <summary>Envía un SMS al número indicado. Lanza en caso de fallo real.</summary>
    Task SendAsync(SmsMessage message, CancellationToken ct = default);
}
