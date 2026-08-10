using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IPermissionService
{
    Task<IEnumerable<PermissionResponse>> GetAllAsync(CancellationToken ct = default);
    Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PermissionResponse?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IEnumerable<PermissionResponse>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task<IEnumerable<PermissionResponse>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetUserAllPermissionCodesAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> AssignToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RemoveFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> AssignToUserAsync(Guid userId, Guid permissionId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RemoveFromUserAsync(Guid userId, Guid permissionId, CancellationToken ct = default);
    Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);
}
