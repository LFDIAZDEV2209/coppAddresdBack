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
}