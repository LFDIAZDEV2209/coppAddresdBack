using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Application.Common;

/// <summary>
/// Fusión (unión pura) de asignaciones scoped: las existentes del usuario más
/// las nuevas del wizard, sin perder el scope de ninguna. El wizard de alta
/// solo agrega; las remociones se hacen en el módulo de Usuarios.
///
/// Claves de deduplicación:
/// <list type="bullet">
/// <item>roles: (roleId, scopeType, scopeId)</item>
/// <item>permisos: (permissionId, scopeType, scopeId, effect)</item>
/// </list>
/// El mismo rol en clínicas distintas queda como asignaciones separadas.
/// </summary>
public static class ScopedAssignmentsMerge
{
    public static (
        IReadOnlyList<ScopedRoleAssignmentInput> Roles,
        IReadOnlyList<ScopedPermissionAssignmentInput> Permissions
    ) Union(
        ScopedAssignmentsResult? current,
        IReadOnlyList<ScopedRoleAssignmentInput> roles,
        IReadOnlyList<ScopedPermissionAssignmentInput> permissions
    )
    {
        // `current` nulo = el usuario no tiene asignaciones o el cliente no
        // pudo leerlas; en ambos casos la unión parte de las nuevas.
        var mergedRoles = (current?.Roles ?? [])
            .Select(r => new ScopedRoleAssignmentInput(r.RoleId, r.ScopeType, r.ScopeId))
            .ToList();

        var roleKeys = mergedRoles
            .Select(r => (r.RoleId, r.ScopeType, r.ScopeId))
            .ToHashSet();

        foreach (var role in roles)
        {
            if (roleKeys.Add((role.RoleId, role.ScopeType, role.ScopeId)))
            {
                mergedRoles.Add(role);
            }
        }

        var mergedPermissions = (current?.Permissions ?? [])
            .Select(p => new ScopedPermissionAssignmentInput(
                p.PermissionId,
                p.ScopeType,
                p.ScopeId,
                p.Effect
            ))
            .ToList();

        var permissionKeys = mergedPermissions
            .Select(p => (p.PermissionId, p.ScopeType, p.ScopeId, p.Effect))
            .ToHashSet();

        foreach (var permission in permissions)
        {
            if (
                permissionKeys.Add(
                    (
                        permission.PermissionId,
                        permission.ScopeType,
                        permission.ScopeId,
                        permission.Effect
                    )
                )
            )
            {
                mergedPermissions.Add(permission);
            }
        }

        return (mergedRoles, mergedPermissions);
    }
}
