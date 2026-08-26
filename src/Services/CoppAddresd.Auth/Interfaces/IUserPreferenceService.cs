using CoppAddresd.Auth.Models;

namespace CoppAddresd.Auth.Interfaces;

public interface IUserPreferenceService
{
    Task<UserPreferenceResponse?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(Guid userId, string lang, CancellationToken ct = default);
}
