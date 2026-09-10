namespace CoppAddresd.Community.Entities;

/// <summary>Mensaje del chat en vivo de una sesión de club.</summary>
public sealed class LiveChatMessage
{
    public Guid Id { get; set; }

    public Guid LiveSessionId { get; set; }

    public Guid SenderProfileId { get; set; }

    public string Body { get; set; } = default!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public LiveSession? LiveSession { get; set; }
    public Profile? SenderProfile { get; set; }
}