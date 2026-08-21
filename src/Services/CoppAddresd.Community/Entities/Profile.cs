namespace CoppAddresd.Community.Entities;

/// <summary>Estado de revisión del perfil de un usuario en la comunidad.</summary>
public enum ProfileStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>Perfil público de un usuario de la comunidad (referencia a auth.users).</summary>
public sealed class Profile
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en el servicio de Auth (auth.users).</summary>
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = default!;

    public string? Bio { get; set; }

    /// <summary>Clave del avatar en el storage (text-only por ahora: NULL).</summary>
    public string? AvatarKey { get; set; }

    public ProfileStatus Status { get; set; } = ProfileStatus.Pending;

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
}
