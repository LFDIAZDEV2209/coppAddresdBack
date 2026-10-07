using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<(bool Success, string? Error, UserResponse? User)> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserEmailAvailabilityResponse> GetEmailAvailabilityAsync(string email, CancellationToken ct = default);
    Task<(bool Success, string? Error, UserResponse? User, bool NotFound)> LinkAsync(LinkUserAccountRequest request, CancellationToken ct = default);
    Task<BulkCreateUsersResult> CreateBulkAsync(BulkCreateUsersRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, bool NotFound)> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, bool NotFound)> DeleteAsync(Guid id, CancellationToken ct = default);
}
