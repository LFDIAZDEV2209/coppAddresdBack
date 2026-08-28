using CoppAddresd.Application.Common;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cliente de Firebase Cloud Messaging (API v1, OAuth2 con service account).
/// Opera degradado cuando <c>Fcm:Enabled</c> es false o faltan credenciales:
/// devuelve <see cref="FcmSendStatus.Disabled"/> sin lanzar excepciones (el
/// endpoint nunca cae por FCM no configurado).
/// </summary>
public interface IFcmClient
{
    /// <summary>
    /// Envía una notificación push a un dispositivo.
    /// </summary>
    /// <param name="deviceToken">Token FCM del dispositivo.</param>
    /// <param name="title">Título de la notificación.</param>
    /// <param name="body">Cuerpo de la notificación.</param>
    /// <param name="data">Metadatos opcionales (p. ej. thread_id, type).</param>
    Task<FcmSendResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
}