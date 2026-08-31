namespace CoppAddresd.Community.Entities;

/// <summary>Voto de un perfil a una opción de una encuesta (uno por encuesta).</summary>
public sealed class PollVote
{
    public Guid Id { get; set; }

    public Guid OptionId { get; set; }

    public Guid ProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PollOption? Option { get; set; }
}
