using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Evaluación de permisos por contexto sobre el modelo scoped (ver
/// <see cref="IScopedPermissionService"/>). Lecturas directas por request:
/// los resultados los cachea el llamador (ERP) keyed por security stamp, así
/// que aquí no hay caché.
/// </summary>
public class ScopedPermissionService(AuthDbContext dbContext) : IScopedPermissionService
{
    public async Task<bool> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default)
    {
        var permissionId = await dbContext.Permissions
            .Where(p => p.Code == permissionCode)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (permissionId is null)
        {
            return false;
        }

        // 1. Permiso global (claims JWT): aplica en cualquier contexto.
        if (await HasGlobalPermissionAsync(userId, permissionId.Value, ct))
        {
            return true;
        }

        // 2. Overrides Grant/Deny: el override del scope más específico gana.
        foreach (var scope in scopeChain)
        {
            var effect = await dbContext.ScopedPermissionAssignments
                .Where(a => a.UserId == userId
                    && a.PermissionId == permissionId.Value
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId)
                .Select(a => a.Effect)
                .FirstOrDefaultAsync(ct);

            if (effect is not null)
            {
                return effect.Equals("Grant", StringComparison.OrdinalIgnoreCase);
            }
        }

        // 3. Roles asignados en el scope o en un ancestro que tengan el permiso.
        foreach (var scope in scopeChain)
        {
            var roleIds = await dbContext.ScopedRoleAssignments
                .Where(a => a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId)
                .Select(a => a.RoleId)
                .ToListAsync(ct);

            if (roleIds.Count == 0)
            {
                continue;
            }

            var hasViaRole = await dbContext.RolePermissions
                .AnyAsync(rp => roleIds.Contains(rp.RoleId) && rp.PermissionId == permissionId.Value, ct);

            if (hasViaRole)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default)
    {
        // Permisos globales: directos + vía roles.
        var allowed = new HashSet<string>(
            await dbContext.UserPermissions
                .Where(up => up.UserId == userId)
                .Select(up => up.Permission.Code)
                .ToListAsync(ct));

        var globalViaRoles = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId)
            .Join(dbContext.Permissions,
                pid => pid,
                p => p.Id,
                (_, p) => p.Code)
            .ToListAsync(ct);
        allowed.UnionWith(globalViaRoles);

        // Roles scoped en cada nivel de la cadena.
        foreach (var scope in scopeChain)
        {
            var roleIds = await dbContext.ScopedRoleAssignments
                .Where(a => a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId)
                .Select(a => a.RoleId)
                .ToListAsync(ct);

            if (roleIds.Count == 0)
            {
                continue;
            }

            var codes = await dbContext.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Code)
                .ToListAsync(ct);
            allowed.UnionWith(codes);
        }

        // Overrides: el del scope más específico gana por permiso.
        var overrides = new List<(string Code, string Effect, int Specificity)>();
        for (var i = 0; i < scopeChain.Count; i++)
        {
            var scope = scopeChain[i];
            var rows = await dbContext.ScopedPermissionAssignments
                .Where(a => a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId)
                .Select(a => new { a.Permission.Code, a.Effect })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                overrides.Add((row.Code, row.Effect, i));
            }
        }

        foreach (var group in overrides.GroupBy(o => o.Code))
        {
            var mostSpecific = group.OrderBy(o => o.Specificity).First();
            if (mostSpecific.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase))
            {
                allowed.Remove(mostSpecific.Code);
            }
            else
            {
                allowed.Add(mostSpecific.Code);
            }
        }

        return allowed.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Permiso global (directo o vía rol sin scope). Es la fuente de los
    /// claims JWT: si existe aquí, el token lo lleva y aplica en todo contexto.
    /// </summary>
    private async Task<bool> HasGlobalPermissionAsync(Guid userId, Guid permissionId, CancellationToken ct)
    {
        var hasDirect = await dbContext.UserPermissions
            .AnyAsync(up => up.UserId == userId && up.PermissionId == permissionId, ct);

        if (hasDirect)
        {
            return true;
        }

        return await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId)
            .AnyAsync(id => id == permissionId, ct);
    }

    public async Task<(bool Success, string? Error)> AssignRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid? grantedBy,
        CancellationToken ct = default)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, ct))
            return (false, "Usuario no encontrado");

        if (!await dbContext.Roles.AnyAsync(r => r.Id == roleId, ct))
            return (false, "Rol no encontrado");

        if (!ValidateScope(scopeType))
            return (false, $"Scope inválido: '{scopeType}'");

        var already = await dbContext.ScopedRoleAssignments
            .AnyAsync(a => a.UserId == userId
                && a.RoleId == roleId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId, ct);

        if (already)
            return (false, "El usuario ya tiene este rol en el scope indicado");

        dbContext.ScopedRoleAssignments.Add(new Entities.ScopedRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            GrantedBy = grantedBy,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default)
    {
        var deleted = await dbContext.ScopedRoleAssignments
            .Where(a => a.UserId == userId
                && a.RoleId == roleId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0
            ? (true, null)
            : (false, "El usuario no tiene este rol en el scope indicado");
    }

    public async Task<(bool Success, string? Error)> AssignPermissionAsync(
        Guid userId,
        Guid permissionId,
        string scopeType,
        Guid? scopeId,
        string effect,
        Guid? grantedBy,
        CancellationToken ct = default)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, ct))
            return (false, "Usuario no encontrado");

        if (!await dbContext.Permissions.AnyAsync(p => p.Id == permissionId, ct))
            return (false, "Permiso no encontrado");

        if (!ValidateScope(scopeType))
            return (false, $"Scope inválido: '{scopeType}'");

        if (!effect.Equals("Grant", StringComparison.OrdinalIgnoreCase)
            && !effect.Equals("Deny", StringComparison.OrdinalIgnoreCase))
            return (false, $"Efecto inválido: '{effect}'. Use Grant o Deny.");

        var already = await dbContext.ScopedPermissionAssignments
            .AnyAsync(a => a.UserId == userId
                && a.PermissionId == permissionId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId, ct);

        if (already)
            return (false, "El usuario ya tiene este override en el scope indicado");

        dbContext.ScopedPermissionAssignments.Add(new Entities.ScopedPermissionAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PermissionId = permissionId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Effect = effect,
            GrantedBy = grantedBy,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemovePermissionAsync(
        Guid userId,
        Guid permissionId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default)
    {
        var deleted = await dbContext.ScopedPermissionAssignments
            .Where(a => a.UserId == userId
                && a.PermissionId == permissionId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0
            ? (true, null)
            : (false, "El usuario no tiene este override en el scope indicado");
    }

    private static bool ValidateScope(string scopeType)
        => ScopeTypes.IsValid(scopeType);
}