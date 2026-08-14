using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<(bool Success, string? Error, UserResponse? User)> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, bool NotFound)> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default);
}
