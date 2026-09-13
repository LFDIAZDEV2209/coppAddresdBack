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
    /// <summary>Suspensión por aplicación; no cambia el estado global del usuario.</summary>
    public bool IsSuspended { get; set; }
    /// <summary>Versión monotónica: las sesiones anteriores no reviven al reactivar.</summary>
    public long SessionVersion { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Application Application { get; set; } = null!;
}
