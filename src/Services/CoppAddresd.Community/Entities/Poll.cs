namespace CoppAddresd.Community.Entities;

/// <summary>Encuesta vinculada 1:1 a una publicación (post.body = pregunta).</summary>
public sealed class Poll
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Post? Post { get; set; }
    public ICollection<PollOption> Options { get; set; } = [];
}
