namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Invitación de un usuario a completar su primer acceso (onboarding).
/// El token se guarda SOLO como hash SHA-256 (nunca en claro); es de un solo
/// uso, expira (72h por defecto) y es revocable. Un usuario puede tener varias
/// invitaciones a lo largo del tiempo, pero solo una pendiente a la vez
/// (crear/reenviar revoca las anteriores).
/// </summary>
public class Invitation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) del token en claro — nunca se persiste el token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;

    public bool IsPending
        => UsedAt is null && RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
