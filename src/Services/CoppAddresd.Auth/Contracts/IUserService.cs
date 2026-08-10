using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Contracts;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<(bool Success, string? Error, UserResponse? User)> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default);
}
