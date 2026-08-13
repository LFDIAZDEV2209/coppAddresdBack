namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Determina explícitamente a qué aplicaciones puede acceder un usuario,
/// independientemente de sus roles o permisos.
/// </summary>
public class UserApplication
{
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Application Application { get; set; } = null!;
}