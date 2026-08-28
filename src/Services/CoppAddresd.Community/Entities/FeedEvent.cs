namespace CoppAddresd.Community.Entities;

/// <summary>
/// Evento del feed en vivo de la comunidad. Puede estar asociado a un perfil (eventos de
/// usuario) o ser nulo para eventos de sistema (ej. otorgamiento masivo de XP).
/// </summary>
public sealed class FeedEvent
{
    public Guid Id { get; set; }

    public Guid? ProfileId { get; set; }

    /// <summary>Tipo de evento (Publicacion, Foto, Racha, Hito, Logro, …).</summary>
    public FeedEventKind Kind { get; set; }

    /// <summary>Texto corto opcional del evento.</summary>
    public string? Body { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profile? Profile { get; set; }
}
