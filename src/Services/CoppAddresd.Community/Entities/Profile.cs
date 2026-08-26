namespace CoppAddresd.Community.Entities;

/// <summary>Estado del perfil de un usuario en la comunidad (sin revisión previa: activo por defecto, baneo posterior).</summary>
public enum ProfileStatus
{
    Active = 1,
    Banned = 2,
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

    public ProfileStatus Status { get; set; } = ProfileStatus.Active;

    public Guid? BannedBy { get; set; }

    public DateTime? BannedAt { get; set; }

    public string? BanReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
}
