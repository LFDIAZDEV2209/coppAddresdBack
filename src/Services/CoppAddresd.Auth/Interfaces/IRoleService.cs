using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IRoleService
{
    Task<IEnumerable<RoleResponse>> GetAllAsync(CancellationToken ct = default);
    Task<RoleResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(bool Success, string? Error, RoleResponse? Role)> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(bool Success, string? Error)> AssignToUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RemoveFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<IEnumerable<RoleResponse>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
}
