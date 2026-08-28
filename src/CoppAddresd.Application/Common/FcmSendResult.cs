namespace CoppAddresd.Application.Common;

/// <summary>
/// Estado final de un envío FCM. <see cref="Disabled"/> representa la operación
/// degradada (FCM no configurado): no es un fallo del dispositivo ni del
/// backend, por lo que no cuenta como enviado ni como error.
/// </summary>
public enum FcmSendStatus
{
    /// <summary>Push enviado y aceptado por FCM.</summary>
    Sent,

    /// <summary>FCM deshabilitado o sin credenciales: no se intentó el envío.</summary>
    Disabled,

    /// <summary>FCM rechazó el mensaje (error transitorio o permanente).</summary>
    Error,

    /// <summary>
    /// Token obsoleto (UNREGISTERED / NotRegistered): el caller debe eliminarlo.
    /// </summary>
    TokenInvalid,
}

/// <summary>
/// Resultado de un envío FCM: estado + código/mensaje de error. La propiedad
/// <see cref="TokenInvalid"/> permite al caller limpiar tokens obsoletos.
/// </summary>
public record FcmSendResult(
    FcmSendStatus Status,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public bool Sent => Status == FcmSendStatus.Sent;

    public bool TokenInvalid => Status == FcmSendStatus.TokenInvalid;
}