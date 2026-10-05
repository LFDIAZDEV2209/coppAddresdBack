namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Sesión de la web de eliminación de cuenta. Vive en una cookie HttpOnly
/// (el navegador nunca ve un token) y solo autoriza eliminar esta cuenta.
/// Se guarda el hash SHA-256 del secreto; se consume una sola vez.
/// </summary>
public class AccountDeletionSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
}
