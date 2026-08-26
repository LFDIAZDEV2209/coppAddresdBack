namespace CoppAddresd.Community.Entities;

/// <summary>Me gusta a una publicación o a un comentario (uno de los dos campos).</summary>
public sealed class Like
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public Guid? PostId { get; set; }

    public Guid? CommentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profile? Profile { get; set; }
    public Post? Post { get; set; }
    public Comment? Comment { get; set; }
}
