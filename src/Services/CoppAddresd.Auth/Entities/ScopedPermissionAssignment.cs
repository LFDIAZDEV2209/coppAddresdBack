namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Excepción (override) de un permiso para un usuario DENTRO de un scope.
/// El cliente es indeciso y requiere excepciones puntuales sin tocar roles:
/// <c>Grant</c> otorga y <c>Deny</c> revoca con precedencia sobre las
/// asignaciones de rol (el Deny más específico gana). El scope_id es null
/// para Global.
/// </summary>
public class ScopedPermissionAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PermissionId { get; set; }

    public string ScopeType { get; set; } = default!;

    public Guid? ScopeId { get; set; }

    /// <summary>Grant | Deny.</summary>
    public string Effect { get; set; } = "Grant";

    public Guid? GrantedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}