namespace CoppAddresd.Application.WhatsApp.Messaging;

public enum WhatsAppFailureKind { Transient, Permanent }

/// <summary>Resultado sin cuerpos del proveedor ni credenciales.</summary>
public sealed record WhatsAppSendResult(string? Wamid, string? ErrorCode = null, WhatsAppFailureKind? Failure = null)
{
    public bool Succeeded => Wamid is not null && Failure is null;
    public static WhatsAppSendResult Failed(string code, WhatsAppFailureKind kind) => new(null, code, kind);
}

public interface IWhatsAppMessageSender
{
    Task<WhatsAppSendResult> SendTextAsync(string waId, string text, DateTimeOffset lastInboundAt, CancellationToken ct);
    Task<WhatsAppSendResult> SendTemplateAsync(string waId, string name, string language,
        IReadOnlyList<string> bodyParameters, CancellationToken ct);
}
