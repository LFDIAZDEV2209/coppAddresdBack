namespace CoppAddresd.Community.Entities;

/// <summary>Sesión en vivo (live) de un club, estilo broadcast (1 → muchos).</summary>
public sealed class LiveSession
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>Evento asociado al live (opcional).</summary>
    public Guid? EventId { get; set; }

    public string Title { get; set; } = default!;

    public DateTime ScheduledStartAt { get; set; }

    public LiveSessionStatus Status { get; set; } = LiveSessionStatus.Programado;

    /// <summary>URL de reproducción embebida (proveedor externo o placeholder).</summary>
    public string? EmbedUrl { get; set; }

    /// <summary>Sala del proveedor de video (p. ej. Twilio) — V3.</summary>
    public string? ProviderRoom { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
    public ClubEvent? Event { get; set; }

    public ICollection<LiveChatMessage> ChatMessages { get; set; } = [];

    /// <summary>Ponentes de la sesión (perfiles de la plataforma).</summary>
    public ICollection<LiveSessionSpeaker> Speakers { get; set; } = [];
}