namespace CoppAddresd.Application.Interfaces;

/// <summary>Rol dentro de un scope (asignación scoped a aplicar).</summary>
public record ScopedRoleAssignmentInput(Guid RoleId, string ScopeType, Guid? ScopeId);

/// <summary>Override Grant/Deny dentro de un scope (asignación scoped a aplicar).</summary>
public record ScopedPermissionAssignmentInput(Guid PermissionId, string ScopeType, Guid? ScopeId, string Effect);

/// <summary>Asignación scoped actual de un rol.</summary>
public record ScopedRoleAssignmentView(Guid RoleId, string RoleName, string ScopeType, Guid? ScopeId);

/// <summary>Override scoped actual de un permiso.</summary>
public record ScopedPermissionAssignmentView(
    Guid PermissionId,
    string PermissionCode,
    string ScopeType,
    Guid? ScopeId,
    string Effect);

/// <summary>Lectura de todas las asignaciones scoped de un usuario.</summary>
public record ScopedAssignmentsResult(
    IReadOnlyList<ScopedRoleAssignmentView> Roles,
    IReadOnlyList<ScopedPermissionAssignmentView> Permissions);

/// <summary>
/// Cliente hacia los endpoints internos de asignaciones scoped del Auth
/// Service (X-Internal-Key). El ERP orquesta la creación del profesional y la
/// gestión de scopes por clínica; el frontend nunca toca el Auth directamente
/// para esto.
/// </summary>
public interface IAuthScopedAssignmentsClient
{
    /// <summary>Asignaciones scoped actuales del usuario (roles + overrides).</summary>
    Task<ScopedAssignmentsResult> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Reemplazo atómico de las asignaciones scoped del usuario. El ERP lo usa
    /// tanto en la creación del profesional como en la gestión desde el detalle.
    /// </summary>
    Task ReplaceAsync(
        Guid userId,
        IReadOnlyList<ScopedRoleAssignmentInput> roles,
        IReadOnlyList<ScopedPermissionAssignmentInput> permissions,
        Guid? grantedBy,
        CancellationToken ct = default);
}