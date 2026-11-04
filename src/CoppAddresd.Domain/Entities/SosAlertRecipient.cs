using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>Destino congelado al activar SOS; cada contacto conserva su entrega e idempotencia.</summary>
public sealed class SosAlertRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertId { get; set; }
    public int ContactOrder { get; set; }
    public string? PhoneE164 { get; set; }
    public string? Email { get; set; }
    public SosChannelStatus SmsChannelStatus { get; set; } = SosChannelStatus.Pendiente;
    public string? SmsDetail { get; set; }
    public string? SmsProviderMessageId { get; set; }
    public string? SmsDeliveryStatus { get; set; }
    public DateTime? SmsUpdatedAt { get; set; }
    public SosChannelStatus VoiceChannelStatus { get; set; } = SosChannelStatus.Pendiente;
    public string? VoiceDetail { get; set; }
    public string? VoiceProviderCallId { get; set; }
    public string? VoiceCallStatus { get; set; }
    public string? VoiceAnsweredBy { get; set; }
    public int? VoiceDurationSeconds { get; set; }
    public DateTime? VoiceUpdatedAt { get; set; }
    public string? EmailChannelStatus { get; set; }
    public DateTime? EmailUpdatedAt { get; set; }
    public SosAlert Alert { get; set; } = null!;
}
