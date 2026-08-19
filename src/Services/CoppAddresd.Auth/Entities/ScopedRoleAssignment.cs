namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Asignación de un rol a un usuario DENTRO de un scope (Global, Organization,
/// Clinic, Location). Extiende el modelo <c>User → Role → Permissions</c>
/// permitiendo que el mismo usuario tenga roles/permisos distintos según el
/// contexto (ej. Nutritionist en Clínica A con edición, solo lectura en B).
/// El scope_id es null para Global; el ancestro se resuelve por la cadena que
/// pasa el llamador (el ERP conoce la jerarquía de clínicas).
/// </summary>
public class ScopedRoleAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public string ScopeType { get; set; } = default!;

    public Guid? ScopeId { get; set; }

    public Guid? GrantedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;

    public virtual ApplicationRole Role { get; set; } = null!;
}