namespace CoppAddresd.Application.Interfaces;
/// <summary>Solo se invoca después de validar firma y número del webhook Meta.</summary>
public interface ISosWhatsAppStatusReceiver
{
    Task ReceiveAsync(string callbackData, string wamid, string recipient, string status, DateTime eventAt, string? errorCode, CancellationToken ct);
}
