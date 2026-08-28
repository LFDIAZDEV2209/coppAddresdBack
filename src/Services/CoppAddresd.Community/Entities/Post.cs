namespace CoppAddresd.Community.Entities;

/// <summary>Publicación de la comunidad (feed social).</summary>
public sealed class Post
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public string Body { get; set; } = default!;

    /// <summary>Clave de almacenamiento de la imagen adjunta (null si el post es solo texto).</summary>
    public string? ImageKey { get; set; }

    /// <summary>Tipo de publicación (Texto, Imagen, Video, Encuesta, Logro). Por defecto Texto.</summary>
    public PostType? Type { get; set; }

    /// <summary>Destino/canal de la publicación. Por defecto TodasLasComunidades.</summary>
    public PostDestination? Destination { get; set; }

    /// <summary>Cantidad de visualizaciones (incrementado por viewPost).</summary>
    public int ViewCount { get; set; }

    public bool Pinned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft delete para moderación.</summary>
    public DateTime? DeletedAt { get; set; }

    public Profile? Profile { get; set; }

    /// <summary>Encuesta vinculada a la publicación (null si es un post normal).</summary>
    public Poll? Poll { get; set; }

    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
    public ICollection<PostReport> Reports { get; set; } = [];
}
