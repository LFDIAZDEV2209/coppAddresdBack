using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services.Cache;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Evaluación de permisos por contexto sobre el modelo scoped (ver
/// <see cref="IScopedPermissionService"/>). Solo se cachea lo catalogable y
/// no por-usuario: el mapeo código→id (inmutable en runtime) y los códigos de
/// permisos por rol (invalidados en las mutaciones de RolePermissions). Las
/// consultas por USUARIO (asignaciones, overrides, globales) van siempre a
/// PostgreSQL: la revocación debe ser inmediata. Los resultados agregados de
/// introspección además se cachean en los llamadores (ERP/Tele) keyed por
/// security stamp. Ver docs/modules/cache/README.md.
/// </summary>
public class ScopedPermissionService(AuthDbContext dbContext, ICacheService cache)
    : IScopedPermissionService
{
    public async Task<bool> AuthorizeAsync(
        Guid userId,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default
    )
    {
        // Transición dual-check simétrico: una petición con un código del rename
        // (nuevo Appointments.* o legado Telemedicine.*) se autoriza si existe un
        // grant de CUALQUIERA de los dos códigos y NO existe un Deny scoped
        // explícito sobre NINGUNO de los dos. Así el Deny no se elude cambiando
        // de nomenclatura: deny(new) bloquea request(legacy) y viceversa. Los
        // códigos sin equivalente (fuera del rename) mantienen el comportamiento
        // de un solo código. Al terminar la transición, este fallback se elimina.
        if (PermissionCodeMap.TryGetLegacyCode(permissionCode, out var legacyCode))
        {
            return await AuthorizeDualAsync(userId, permissionCode, legacyCode, scopeChain, ct);
        }

        if (PermissionCodeMap.TryGetNewCode(permissionCode, out var newCode))
        {
            return await AuthorizeDualAsync(userId, permissionCode, newCode, scopeChain, ct);
        }

        return await AuthorizeSingleAsync(userId, permissionCode, scopeChain, ct);
    }

    /// <summary>
    /// Autorización con el gemelo del rename (nuevo ↔ legado): se concede si el
    /// grant existe para CUALQUIERA de los dos códigos y un Deny scoped explícito
    /// no bloquea NINGUNO de los dos. El Deny sobre cualquiera de los dos códigos
    /// prevalece sobre el fallback del gemelo, en ambas direcciones.
    /// </summary>
    private async Task<bool> AuthorizeDualAsync(
        Guid userId,
        string primaryCode,
        string twinCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct
    )
    {
        if (
            await HasScopedDenyAsync(userId, primaryCode, scopeChain, ct)
            || await HasScopedDenyAsync(userId, twinCode, scopeChain, ct)
        )
        {
            return false;
        }

        return await AuthorizeSingleAsync(userId, primaryCode, scopeChain, ct)
            || await AuthorizeSingleAsync(userId, twinCode, scopeChain, ct);
    }

    private async Task<bool> AuthorizeSingleAsync(
        Guid userId,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct
    )
    {
        var permissionId = await GetPermissionIdAsync(permissionCode, ct);

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
            var effect = await dbContext
                .ScopedPermissionAssignments.Where(a =>
                    a.UserId == userId
                    && a.PermissionId == permissionId.Value
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId
                )
                .Select(a => a.Effect)
                .FirstOrDefaultAsync(ct);

            if (effect is not null)
            {
                return effect.Equals("Grant", StringComparison.OrdinalIgnoreCase);
            }
        }

        // 3. Roles asignados en el scope o en un ancestro que tengan el permiso
        //    (códigos por rol cacheados; equivalente al join por PermissionId
        //    porque el mapeo código↔id es único).
        foreach (var scope in scopeChain)
        {
            var roleIds = await dbContext
                .ScopedRoleAssignments.Where(a =>
                    a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId
                )
                .Select(a => a.RoleId)
                .ToListAsync(ct);

            if (roleIds.Count == 0)
            {
                continue;
            }

            foreach (var roleId in roleIds)
            {
                var codes = await GetRolePermissionCodesAsync(roleId, ct);
                if (codes.Contains(permissionCode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// ¿Existe un override scoped Deny explícito para el código en la cadena?
    /// Durante el dual-check simétrico, un Deny sobre cualquiera de los dos
    /// códigos del rename (nuevo o legado) bloquea la petición, incluso si el
    /// grant existe para el otro.
    /// </summary>
    private async Task<bool> HasScopedDenyAsync(
        Guid userId,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct
    )
    {
        var permissionId = await GetPermissionIdAsync(permissionCode, ct);

        if (permissionId is null)
        {
            return false;
        }

        foreach (var scope in scopeChain)
        {
            var effect = await dbContext
                .ScopedPermissionAssignments.Where(a =>
                    a.UserId == userId
                    && a.PermissionId == permissionId.Value
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId
                )
                .Select(a => a.Effect)
                .FirstOrDefaultAsync(ct);

            if (effect is not null)
            {
                return effect.Equals("Deny", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default
    )
    {
        // Permisos globales: directos + vía roles.
        var allowed = new HashSet<string>(
            await dbContext
                .UserPermissions.Where(up => up.UserId == userId)
                .Select(up => up.Permission.Code)
                .ToListAsync(ct)
        );

        var globalViaRoles = await dbContext
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(
                dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId
            )
            .Join(dbContext.Permissions, pid => pid, p => p.Id, (_, p) => p.Code)
            .ToListAsync(ct);
        allowed.UnionWith(globalViaRoles);

        // Roles scoped en cada nivel de la cadena.
        foreach (var scope in scopeChain)
        {
            var roleIds = await dbContext
                .ScopedRoleAssignments.Where(a =>
                    a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId
                )
                .Select(a => a.RoleId)
                .ToListAsync(ct);

            if (roleIds.Count == 0)
            {
                continue;
            }

            foreach (var roleId in roleIds)
            {
                allowed.UnionWith(await GetRolePermissionCodesAsync(roleId, ct));
            }
        }

        // Overrides: el del scope más específico gana por permiso.
        var overrides = new List<(string Code, string Effect, int Specificity)>();
        for (var i = 0; i < scopeChain.Count; i++)
        {
            var scope = scopeChain[i];
            var rows = await dbContext
                .ScopedPermissionAssignments.Where(a =>
                    a.UserId == userId
                    && a.ScopeType == scope.ScopeType
                    && a.ScopeId == scope.ScopeId
                )
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
    private async Task<bool> HasGlobalPermissionAsync(
        Guid userId,
        Guid permissionId,
        CancellationToken ct
    )
    {
        var hasDirect = await dbContext.UserPermissions.AnyAsync(
            up => up.UserId == userId && up.PermissionId == permissionId,
            ct
        );

        if (hasDirect)
        {
            return true;
        }

        return await dbContext
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(
                dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId
            )
            .AnyAsync(id => id == permissionId, ct);
    }

    /// <summary>
    /// Mapeo código→id cacheado (auth:permits:code:{código}:v1, TTL 24h): el
    /// catálogo de permisos es inmutable en runtime (solo seeders), así que la
    /// clave no requiere invalidación activa. Fail-open: con el caché caído
    /// consulta PostgreSQL igual que antes.
    /// </summary>
    private async Task<Guid?> GetPermissionIdAsync(string permissionCode, CancellationToken ct)
    {
        var cached = await cache.GetAsync<string>(AuthCacheKeys.PermissionId(permissionCode), ct);
        if (cached is not null && Guid.TryParse(cached, out var parsed))
        {
            return parsed;
        }

        var permissionId = await dbContext
            .Permissions.Where(p => p.Code == permissionCode)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (permissionId is { } id)
        {
            await cache.SetAsync(
                AuthCacheKeys.PermissionId(permissionCode),
                id.ToString(),
                AuthCacheKeys.PermissionIdTtl,
                ct
            );
        }

        return permissionId;
    }

    /// <summary>
    /// Códigos de permisos de un rol, cacheados (auth:roles:{roleId}:codes:v1,
    /// TTL 15 min alineado con la vida del access token + invalidación activa
    /// en AssignToRole/RemoveFromRole del PermissionService).
    /// </summary>
    private async Task<IReadOnlyList<string>> GetRolePermissionCodesAsync(
        Guid roleId,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            AuthCacheKeys.RoleCodes(roleId),
            AuthCacheKeys.RoleCodesTtl,
            async token =>
                (IReadOnlyList<string>)
                    await dbContext
                        .RolePermissions.Where(rp => rp.RoleId == roleId)
                        .Join(
                            dbContext.Permissions,
                            rp => rp.PermissionId,
                            p => p.Id,
                            (rp, p) => p.Code
                        )
                        .ToListAsync(token),
            ct
        );
    }

    public async Task<(bool Success, string? Error)> AssignRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid? grantedBy,
        CancellationToken ct = default
    )
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, ct))
            return (false, "Usuario no encontrado");

        if (!await dbContext.Roles.AnyAsync(r => r.Id == roleId, ct))
            return (false, "Rol no encontrado");

        if (!ValidateScope(scopeType))
            return (false, $"Scope inválido: '{scopeType}'");

        var already = await dbContext.ScopedRoleAssignments.AnyAsync(
            a =>
                a.UserId == userId
                && a.RoleId == roleId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId,
            ct
        );

        if (already)
            return (false, "El usuario ya tiene este rol en el scope indicado");

        dbContext.ScopedRoleAssignments.Add(
            new Entities.ScopedRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = roleId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                GrantedBy = grantedBy,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default
    )
    {
        var deleted = await dbContext
            .ScopedRoleAssignments.Where(a =>
                a.UserId == userId
                && a.RoleId == roleId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId
            )
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
        CancellationToken ct = default
    )
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, ct))
            return (false, "Usuario no encontrado");

        if (!await dbContext.Permissions.AnyAsync(p => p.Id == permissionId, ct))
            return (false, "Permiso no encontrado");

        if (!ValidateScope(scopeType))
            return (false, $"Scope inválido: '{scopeType}'");

        if (
            !effect.Equals("Grant", StringComparison.OrdinalIgnoreCase)
            && !effect.Equals("Deny", StringComparison.OrdinalIgnoreCase)
        )
            return (false, $"Efecto inválido: '{effect}'. Use Grant o Deny.");

        var already = await dbContext.ScopedPermissionAssignments.AnyAsync(
            a =>
                a.UserId == userId
                && a.PermissionId == permissionId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId,
            ct
        );

        if (already)
            return (false, "El usuario ya tiene este override en el scope indicado");

        dbContext.ScopedPermissionAssignments.Add(
            new Entities.ScopedPermissionAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PermissionId = permissionId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                Effect = effect,
                GrantedBy = grantedBy,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemovePermissionAsync(
        Guid userId,
        Guid permissionId,
        string scopeType,
        Guid? scopeId,
        CancellationToken ct = default
    )
    {
        var deleted = await dbContext
            .ScopedPermissionAssignments.Where(a =>
                a.UserId == userId
                && a.PermissionId == permissionId
                && a.ScopeType == scopeType
                && a.ScopeId == scopeId
            )
            .ExecuteDeleteAsync(ct);

        return deleted > 0
            ? (true, null)
            : (false, "El usuario no tiene este override en el scope indicado");
    }

    private static bool ValidateScope(string scopeType) => ScopeTypes.IsValid(scopeType);

    public async Task<ScopedAssignmentsSnapshot> GetAssignmentsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var roles = await dbContext
            .ScopedRoleAssignments.Where(a => a.UserId == userId)
            .Select(a => new ScopedRoleView(a.RoleId, a.Role.Name, a.ScopeType, a.ScopeId))
            .ToListAsync(ct);

        var permissions = await dbContext
            .ScopedPermissionAssignments.Where(a => a.UserId == userId)
            .Select(a => new ScopedPermissionView(
                a.PermissionId,
                a.Permission.Code,
                a.ScopeType,
                a.ScopeId,
                a.Effect
            ))
            .ToListAsync(ct);

        return new ScopedAssignmentsSnapshot(roles, permissions);
    }

    public async Task<(bool Success, string? Error)> ReplaceAssignmentsAsync(
        Guid userId,
        IReadOnlyList<ScopedRoleInput> roles,
        IReadOnlyList<ScopedPermissionInput> permissions,
        Guid? grantedBy,
        CancellationToken ct = default
    )
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId, ct))
            return (false, "Usuario no encontrado");

        // Validación en lote antes de mutar nada (falla rápido).
        foreach (var role in roles)
        {
            if (!await dbContext.Roles.AnyAsync(r => r.Id == role.RoleId, ct))
                return (false, $"Rol no encontrado: {role.RoleId}");
            if (!ValidateScope(role.ScopeType))
                return (false, $"Scope inválido: '{role.ScopeType}'");
        }

        foreach (var permission in permissions)
        {
            if (!await dbContext.Permissions.AnyAsync(p => p.Id == permission.PermissionId, ct))
                return (false, $"Permiso no encontrado: {permission.PermissionId}");
            if (!ValidateScope(permission.ScopeType))
                return (false, $"Scope inválido: '{permission.ScopeType}'");
            if (
                !permission.Effect.Equals("Grant", StringComparison.OrdinalIgnoreCase)
                && !permission.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase)
            )
                return (false, $"Efecto inválido: '{permission.Effect}'. Use Grant o Deny.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var wantedRoles = roles.Select(r => (r.RoleId, r.ScopeType, r.ScopeId)).ToHashSet();
        var currentRoles = await dbContext
            .ScopedRoleAssignments.Where(a => a.UserId == userId)
            .ToListAsync(ct);

        dbContext.ScopedRoleAssignments.RemoveRange(
            currentRoles.Where(a => !wantedRoles.Contains((a.RoleId, a.ScopeType, a.ScopeId)))
        );

        var existingRoleKeys = currentRoles
            .Select(a => (a.RoleId, a.ScopeType, a.ScopeId))
            .ToHashSet();
        foreach (var role in roles)
        {
            if (existingRoleKeys.Contains((role.RoleId, role.ScopeType, role.ScopeId)))
                continue;

            dbContext.ScopedRoleAssignments.Add(
                new Entities.ScopedRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = role.RoleId,
                    ScopeType = role.ScopeType,
                    ScopeId = role.ScopeId,
                    GrantedBy = grantedBy,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        var wantedPermissions = permissions
            .Select(p => (p.PermissionId, p.ScopeType, p.ScopeId))
            .ToHashSet();
        var currentPermissions = await dbContext
            .ScopedPermissionAssignments.Where(a => a.UserId == userId)
            .ToListAsync(ct);

        dbContext.ScopedPermissionAssignments.RemoveRange(
            currentPermissions.Where(a =>
                !wantedPermissions.Contains((a.PermissionId, a.ScopeType, a.ScopeId))
            )
        );

        var existingPermissionKeys = currentPermissions
            .Select(a => (a.PermissionId, a.ScopeType, a.ScopeId))
            .ToHashSet();
        foreach (var permission in permissions)
        {
            if (
                existingPermissionKeys.Contains(
                    (permission.PermissionId, permission.ScopeType, permission.ScopeId)
                )
            )
                continue;

            dbContext.ScopedPermissionAssignments.Add(
                new Entities.ScopedPermissionAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PermissionId = permission.PermissionId,
                    ScopeType = permission.ScopeType,
                    ScopeId = permission.ScopeId,
                    Effect = permission.Effect,
                    GrantedBy = grantedBy,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (true, null);
    }
}
