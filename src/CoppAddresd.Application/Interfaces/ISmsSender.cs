namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resultado del envío de un SMS (SPEC A13).
/// </summary>
/// <param name="Success">Indica si el proveedor aceptó el mensaje.</param>
/// <param name="ProviderMessageId">Identificador devuelto por el proveedor.</param>
/// <param name="Error">Detalle del fallo (null si fue exitoso).</param>
public sealed record SmsSendResult(bool Success, string? ProviderMessageId, string? Error);

/// <summary>
/// Abstracción de envío de SMS (SPEC A13). La primera versión usa una
/// implementación <c>Noop</c> que registra en log; el proveedor real (Twilio
/// Messages) se integra después sin cambiar los handlers.
/// </summary>
public interface ISmsSender
{
    /// <summary>Nombre del proveedor activo (ej. <c>noop</c>, <c>twilio</c>).</summary>
    string Provider { get; }

    /// <summary>Indica si el canal está configurado para enviar realmente.</summary>
    bool IsConfigured { get; }

    Task<SmsSendResult> SendAsync(string phoneNumber, string body, CancellationToken ct = default);
}
