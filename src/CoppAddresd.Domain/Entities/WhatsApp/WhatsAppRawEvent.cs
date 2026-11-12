namespace CoppAddresd.Domain.Entities.WhatsApp;

/// <summary>Entrada durable del canal; el identificador de Meta evita procesar reenvíos.</summary>
public sealed class WhatsAppRawEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Wamid { get; set; } = "";
    public string Payload { get; set; } = "";
    public string WaId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset MessageAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public int DuplicateCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? Reply { get; set; }
    public int SentFragments { get; set; }
}
