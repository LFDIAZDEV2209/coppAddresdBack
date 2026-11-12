namespace CoppAddresd.Domain.Entities.WhatsApp;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public string ThreadId { get; set; } = "whatsapp-" + Guid.NewGuid().ToString("N");
    public string Language { get; set; } = "es";
    public DateTimeOffset LastInboundAt { get; set; }
}
