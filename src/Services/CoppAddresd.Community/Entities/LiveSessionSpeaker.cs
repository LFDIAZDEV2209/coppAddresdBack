namespace CoppAddresd.Community.Entities;

/// <summary>Ponente de una sesión en vivo (perfil de la plataforma).</summary>
public sealed class LiveSessionSpeaker
{
    public Guid LiveSessionId { get; set; }

    public Guid ProfileId { get; set; }

    public LiveSession? LiveSession { get; set; }
    public Profile? Profile { get; set; }
}