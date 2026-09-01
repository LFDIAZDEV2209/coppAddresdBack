namespace CoppAddresd.Community.Entities;

/// <summary>Repost de una publicación por parte de un usuario.</summary>
public sealed class Repost
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public Guid ProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegaciones
    public Post? Post { get; set; }
    public Profile? Profile { get; set; }
}
