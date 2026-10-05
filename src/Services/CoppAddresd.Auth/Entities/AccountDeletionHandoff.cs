namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Código de un solo uso con el que la app móvil entrega la sesión a la web de
/// eliminación de cuenta sin poner un token en la URL. Solo se guarda el hash
/// SHA-256 del código; caduca en segundos y se consume de forma atómica.
/// </summary>
public class AccountDeletionHandoff
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
}
