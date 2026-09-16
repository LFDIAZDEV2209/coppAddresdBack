namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resultado del envío de un mensaje directo por Community (SPEC A13).
/// </summary>
/// <param name="Success">Indica si Community aceptó el mensaje.</param>
/// <param name="ProviderMessageId">Identificador del mensaje creado.</param>
/// <param name="Error">Detalle del fallo (null si fue exitoso).</param>
/// <param name="SkipReason">Razón por la que no se envió (ej. sin cuenta).</param>
public sealed record CommunityMessageResult(
    bool Success,
    string? ProviderMessageId,
    string? Error,
    string? SkipReason = null);

/// <summary>
/// Canal de mensajería hacia la app del paciente a través del servicio
/// Community (SPEC A13). El ERP resuelve el perfil comunitario por el
/// <c>UserId</c> del paciente y entrega el mensaje como remitente de sistema.
/// </summary>
public interface ICommunityMessageSender
{
    /// <summary>Nombre del proveedor activo (ej. <c>community</c>).</summary>
    string Provider { get; }

    /// <summary>Indica si el canal está configurado (BaseUrl + clave interna).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Envía un mensaje directo al paciente. <paramref name="patientUserId"/> es
    /// el <c>auth.users</c> del paciente (null → se omite con razón).
    /// </summary>
    Task<CommunityMessageResult> SendDirectMessageAsync(
        Guid? patientUserId,
        string body,
        Guid? actorUserId,
        CancellationToken ct = default);
}
