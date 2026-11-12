namespace CoppAddresd.Application.WhatsApp.Messaging;

public enum WhatsAppFailureKind
{
    Transient,
    Permanent,
}

/// <summary>Resultado sin cuerpos del proveedor ni credenciales.</summary>
public sealed record WhatsAppSendResult(
    string? Wamid,
    string? ErrorCode = null,
    WhatsAppFailureKind? Failure = null
)
{
    public bool Succeeded => Wamid is not null && Failure is null;

    public static WhatsAppSendResult Failed(string code, WhatsAppFailureKind kind) =>
        new(null, code, kind);
}

public interface IWhatsAppMessageSender
{
    Task<WhatsAppSendResult> SendTextAsync(
        string waId,
        string text,
        DateTimeOffset lastInboundAt,
        CancellationToken ct
    );
    Task<WhatsAppSendResult> SendTemplateAsync(
        string waId,
        string name,
        string language,
        IReadOnlyList<string> bodyParameters,
        CancellationToken ct
    );

    /// <summary>
    /// Marca el mensaje entrante como leído y activa el indicador de escritura
    /// de WhatsApp (tres puntos). Cosmético: nunca lanza, los fallos se
    /// registran y se ignoran para no romper el procesamiento del evento.
    /// </summary>
    Task ShowTypingAsync(string inboundWamid, CancellationToken ct);
}
