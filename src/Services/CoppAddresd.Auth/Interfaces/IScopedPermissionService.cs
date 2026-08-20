using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Evaluación de permisos por contexto (scoped). El modelo extiende
/// <c>User → Role → Permissions</c> con:
///  - Roles asignados dentro de un scope (<see cref="Entities.ScopedRoleAssignment"/>).
///  - Excepciones Grant/Deny por usuario dentro de un scope
///    (<see cref="Entities.ScopedPermissionAssignment"/>).
/// La jerarquía se resuelve con la cadena de scopes que pasa el llamador
/// (más específica → Global). Los permisos globales (claims JWT) siempre
/// aplican: quien tiene el permiso global lo tiene en cualquier contexto.
/// </summary>
public interface IScopedPermissionService
{
    /// <summary>true si el usuario tiene <paramref name="permissionCode"/> en la cadena de scopes (o global).</summary>
    Task<bool> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default);

    /// <summary>Códigos de permiso efectivos del usuario para la cadena de scopes (global ∪ scoped ∪ overrides).</summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> AssignRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid? grantedBy,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> RemoveRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> AssignPermissionAsync(
        Guid userId,
        Guid permissionId,
        string scopeType,
        Guid? scopeId,
        string effect,
        Guid? grantedBy,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> RemovePermissionAsync(
        Guid userId,
        Guid permissionId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default);

    /// <summary>Lectura de TODAS las asignaciones scoped del usuario (roles + overrides).</summary>
    Task<ScopedAssignmentsSnapshot> GetAssignmentsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Reemplazo atómico de las asignaciones scoped del usuario en UNA
    /// transacción: elimina las actuales que no estén en la lista deseada y
    /// agrega las faltantes. Lo usa el ERP para orquestar la creación del
    /// profesional y para la gestión de scopes desde el detalle.
    /// </summary>
    Task<(bool Success, string? Error)> ReplaceAssignmentsAsync(
        Guid userId,
        IReadOnlyList<ScopedRoleInput> roles,
        IReadOnlyList<ScopedPermissionInput> permissions,
        Guid? grantedBy,
        CancellationToken ct = default);
}

/// <summary>Rol dentro de un scope (entrada para reemplazo).</summary>
public record ScopedRoleInput(Guid RoleId, string ScopeType, Guid? ScopeId);

/// <summary>Override Grant/Deny dentro de un scope (entrada para reemplazo).</summary>
public record ScopedPermissionInput(Guid PermissionId, string ScopeType, Guid? ScopeId, string Effect);

/// <summary>Lectura de las asignaciones scoped actuales de un usuario.</summary>
public record ScopedAssignmentsSnapshot(
    IReadOnlyList<ScopedRoleView> Roles,
    IReadOnlyList<ScopedPermissionView> Permissions);

/// <summary>Rol scoped con nombre (para la UI de gestión).</summary>
public record ScopedRoleView(Guid RoleId, string RoleName, string ScopeType, Guid? ScopeId);

/// <summary>Override scoped con código de permiso (para la UI de gestión).</summary>
public record ScopedPermissionView(Guid PermissionId, string PermissionCode, string ScopeType, Guid? ScopeId, string Effect);