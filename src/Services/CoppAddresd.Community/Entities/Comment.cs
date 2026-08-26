namespace CoppAddresd.Community.Entities;

/// <summary>Comentario o respuesta a un comentario (parent_comment_id).</summary>
public sealed class Comment
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public Guid ProfileId { get; set; }

    /// <summary>NULL = comentario raíz; distinto de NULL = respuesta.</summary>
    public Guid? ParentCommentId { get; set; }

    public string Body { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public Post? Post { get; set; }
    public Profile? Profile { get; set; }
    public ICollection<Comment> Replies { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
}
