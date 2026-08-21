namespace CoppAddresd.Community.Entities;

/// <summary>Publicación de la comunidad (feed social).</summary>
public sealed class Post
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public string Body { get; set; } = default!;

    public bool Pinned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft delete para moderación.</summary>
    public DateTime? DeletedAt { get; set; }

    public Profile? Profile { get; set; }
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
}
