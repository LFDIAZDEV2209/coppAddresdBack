using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services.Cache;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

public class PermissionService : IPermissionService
{
    private readonly AuthDbContext _dbContext;
    private readonly ITokenInvalidationService _tokenInvalidation;
    private readonly ICacheService _cache;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        AuthDbContext dbContext,
        ITokenInvalidationService tokenInvalidation,
        ICacheService cache,
        ILogger<PermissionService> logger
    )
    {
        _dbContext = dbContext;
        _tokenInvalidation = tokenInvalidation;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<PermissionResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var permissions = await _dbContext
            .Permissions.OrderBy(p => p.Module)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionResponse(
                p.Id.ToString(),
                p.Code,
                p.Name,
                p.Description,
                p.Module,
                p.CreatedAt
            ))
            .ToListAsync(ct);

        return permissions;
    }

    public async Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _dbContext
            .Permissions.Where(p => p.Id == id)
            .Select(p => new PermissionResponse(
                p.Id.ToString(),
                p.Code,
                p.Name,
                p.Description,
                p.Module,
                p.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);

        return permission;
    }

    public async Task<PermissionResponse?> GetByCodeAsync(
        string code,
        CancellationToken ct = default
    )
    {
        var permission = await _dbContext
            .Permissions.Where(p => p.Code == code)
            .Select(p => new PermissionResponse(
                p.Id.ToString(),
                p.Code,
                p.Name,
                p.Description,
                p.Module,
                p.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);

        return permission;
    }

    public async Task<IEnumerable<PermissionResponse>> GetRolePermissionsAsync(
        Guid roleId,
        CancellationToken ct = default
    )
    {
        var permissions = await _dbContext
            .RolePermissions.Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionResponse(
                p.Id.ToString(),
                p.Code,
                p.Name,
                p.Description,
                p.Module,
                p.CreatedAt
            ))
            .ToListAsync(ct);

        return permissions;
    }

    public async Task<IEnumerable<PermissionResponse>> GetUserPermissionsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var permissions = await _dbContext
            .UserPermissions.Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionResponse(
                p.Id.ToString(),
                p.Code,
                p.Name,
                p.Description,
                p.Module,
                p.CreatedAt
            ))
            .ToListAsync(ct);

        return permissions;
    }

    public async Task<(bool Success, string? Error)> AssignToRoleAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken ct = default
    )
    {
        var roleExists = await _dbContext.Roles.AnyAsync(r => r.Id == roleId, ct);
        if (!roleExists)
        {
            return (false, "Rol no encontrado");
        }

        var permissionExists = await _dbContext.Permissions.AnyAsync(p => p.Id == permissionId, ct);
        if (!permissionExists)
        {
            return (false, "Permiso no encontrado");
        }

        var alreadyAssigned = await _dbContext.RolePermissions.AnyAsync(
            rp => rp.RoleId == roleId && rp.PermissionId == permissionId,
            ct
        );

        if (alreadyAssigned)
        {
            return (false, "El rol ya tiene este permiso");
        }

        var rolePermission = new Entities.RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
        };

        _dbContext.RolePermissions.Add(rolePermission);
        await _dbContext.SaveChangesAsync(ct);

        var affectedUserIds = await _dbContext
            .UserRoles.Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        // Batching (REQ-INVALID-05): un solo UPDATE para todos los usuarios
        // del rol; antes era un loop con 2 round trips por usuario y sin ct.
        await _tokenInvalidation.InvalidateUsersTokensAsync(affectedUserIds, ct);

        // Invalidación del caché de códigos por rol: la siguiente introspección
        // reconstruye con el permiso ya asignado.
        await _cache.RemoveAsync(AuthCacheKeys.RoleCodes(roleId), ct);

        _logger.LogInformation(
            "Permission {PermissionId} assigned to role {RoleId}",
            permissionId,
            roleId
        );
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveFromRoleAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken ct = default
    )
    {
        var rolePermission = await _dbContext.RolePermissions.FirstOrDefaultAsync(
            rp => rp.RoleId == roleId && rp.PermissionId == permissionId,
            ct
        );

        if (rolePermission is null)
        {
            return (false, "El rol no tiene este permiso");
        }

        _dbContext.RolePermissions.Remove(rolePermission);
        await _dbContext.SaveChangesAsync(ct);

        var affectedUserIds = await _dbContext
            .UserRoles.Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        // Batching (REQ-INVALID-05): un solo UPDATE para todos los usuarios
        // del rol; antes era un loop con 2 round trips por usuario y sin ct.
        await _tokenInvalidation.InvalidateUsersTokensAsync(affectedUserIds, ct);

        // Invalidación del caché de códigos por rol.
        await _cache.RemoveAsync(AuthCacheKeys.RoleCodes(roleId), ct);

        _logger.LogInformation(
            "Permission {PermissionId} removed from role {RoleId}",
            permissionId,
            roleId
        );
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetForRoleAsync(
        Guid roleId,
        IReadOnlyList<Guid> permissionIds,
        CancellationToken ct = default
    )
    {
        // Validar que el rol exista.
        var roleExists = await _dbContext.Roles.AnyAsync(r => r.Id == roleId, ct);
        if (!roleExists)
        {
            return (false, "Rol no encontrado");
        }

        // Validar que TODOS los permisos existan (el set completo, no individualmente).
        if (permissionIds.Count > 0)
        {
            var existingCount = await _dbContext
                .Permissions.Where(p => permissionIds.Contains(p.Id))
                .CountAsync(ct);

            if (existingCount != permissionIds.Count)
            {
                return (false, "Permiso no encontrado");
            }
        }

        // Obtener el set actual de permisos del rol.
        var currentPermissionIds = await _dbContext
            .RolePermissions.Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

        var targetSet = new HashSet<Guid>(permissionIds);
        var currentSet = new HashSet<Guid>(currentPermissionIds);

        // Calcular diffs: permisos a agregar y a remover.
        var toAdd = targetSet.Except(currentSet).ToList();
        var toRemove = currentSet.Except(targetSet).ToList();

        // Si no hay cambios, es un no-op idempotente.
        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            return (true, null);
        }

        // Agregar permisos faltantes.
        foreach (var permId in toAdd)
        {
            _dbContext.RolePermissions.Add(
                new Entities.RolePermission { RoleId = roleId, PermissionId = permId }
            );
        }

        // Remover permisos sobrantes.
        if (toRemove.Count > 0)
        {
            var toRemoveEntities = await _dbContext
                .RolePermissions.Where(rp =>
                    rp.RoleId == roleId && toRemove.Contains(rp.PermissionId)
                )
                .ToListAsync(ct);

            _dbContext.RolePermissions.RemoveRange(toRemoveEntities);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Invalidación de tokens de todos los usuarios afectados por el rol.
        var affectedUserIds = await _dbContext
            .UserRoles.Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (affectedUserIds.Count > 0)
        {
            await _tokenInvalidation.InvalidateUsersTokensAsync(affectedUserIds, ct);
        }

        // Invalidación del caché de códigos por rol.
        await _cache.RemoveAsync(AuthCacheKeys.RoleCodes(roleId), ct);

        _logger.LogInformation(
            "Permissions synced for role {RoleId}: added {Added}, removed {Removed}",
            roleId,
            toAdd.Count,
            toRemove.Count
        );
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignToUserAsync(
        Guid userId,
        Guid permissionId,
        CancellationToken ct = default
    )
    {
        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
        {
            return (false, "Usuario no encontrado");
        }

        var permissionExists = await _dbContext.Permissions.AnyAsync(p => p.Id == permissionId, ct);
        if (!permissionExists)
        {
            return (false, "Permiso no encontrado");
        }

        var alreadyAssigned = await _dbContext.UserPermissions.AnyAsync(
            up => up.UserId == userId && up.PermissionId == permissionId,
            ct
        );

        if (alreadyAssigned)
        {
            return (false, "El usuario ya tiene este permiso");
        }

        var userPermission = new Entities.UserPermission
        {
            UserId = userId,
            PermissionId = permissionId,
        };

        _dbContext.UserPermissions.Add(userPermission);
        await _dbContext.SaveChangesAsync(ct);

        await _tokenInvalidation.InvalidateUserTokensAsync(userId, ct);

        _logger.LogInformation(
            "Permission {PermissionId} assigned to user {UserId}",
            permissionId,
            userId
        );
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveFromUserAsync(
        Guid userId,
        Guid permissionId,
        CancellationToken ct = default
    )
    {
        var userPermission = await _dbContext.UserPermissions.FirstOrDefaultAsync(
            up => up.UserId == userId && up.PermissionId == permissionId,
            ct
        );

        if (userPermission is null)
        {
            return (false, "El usuario no tiene este permiso");
        }

        _dbContext.UserPermissions.Remove(userPermission);
        await _dbContext.SaveChangesAsync(ct);

        await _tokenInvalidation.InvalidateUserTokensAsync(userId, ct);

        _logger.LogInformation(
            "Permission {PermissionId} removed from user {UserId}",
            permissionId,
            userId
        );
        return (true, null);
    }

    public async Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionCode,
        CancellationToken ct = default
    )
    {
        var permission = await _dbContext.Permissions.FirstOrDefaultAsync(
            p => p.Code == permissionCode,
            ct
        );

        if (permission is null)
        {
            return false;
        }

        var hasDirectPermission = await _dbContext.UserPermissions.AnyAsync(
            up => up.UserId == userId && up.PermissionId == permission.Id,
            ct
        );

        if (hasDirectPermission)
        {
            return true;
        }

        var hasPermissionThroughRole = await _dbContext
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(
                _dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId
            )
            .AnyAsync(permissionId => permissionId == permission.Id, ct);

        return hasPermissionThroughRole;
    }

    public async Task<IEnumerable<string>> GetUserAllPermissionCodesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var directPermissions = await _dbContext
            .UserPermissions.Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .ToListAsync(ct);

        var rolePermissions = await _dbContext
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(
                _dbContext.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId
            )
            .Join(_dbContext.Permissions, permissionId => permissionId, p => p.Id, (_, p) => p.Code)
            .ToListAsync(ct);

        return directPermissions.Concat(rolePermissions).Distinct();
    }

    /// <summary>
    /// Union de <see cref="GetUserAllPermissionCodesAsync"/> mas los permisos de
    /// los roles con scope (ScopedRoleAssignments -> RolePermissions), en TODOS
    /// los scopes. Solo para gating de UI (endpoint <c>/api/auth/me</c>): el
    /// scope se sigue evaluando por request en el backend. No emitir en JWT.
    /// </summary>
    public async Task<IEnumerable<string>> GetUserEffectivePermissionCodesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var strict = await GetUserAllPermissionCodesAsync(userId, ct);

        var scoped = await _dbContext
            .ScopedRoleAssignments.Where(a => a.UserId == userId)
            .Join(
                _dbContext.RolePermissions,
                a => a.RoleId,
                rp => rp.RoleId,
                (a, rp) => rp.PermissionId
            )
            .Join(_dbContext.Permissions, permissionId => permissionId, p => p.Id, (_, p) => p.Code)
            .ToListAsync(ct);

        return strict.Concat(scoped).Distinct();
    }
}
