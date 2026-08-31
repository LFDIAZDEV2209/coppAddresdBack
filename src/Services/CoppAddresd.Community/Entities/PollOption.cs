namespace CoppAddresd.Community.Entities;

/// <summary>Opción de respuesta de una encuesta (posición para el ordenamiento).</summary>
public sealed class PollOption
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    public string Text { get; set; } = default!;

    public int Position { get; set; }

    public Poll? Poll { get; set; }
    public ICollection<PollVote> Votes { get; set; } = [];
}
