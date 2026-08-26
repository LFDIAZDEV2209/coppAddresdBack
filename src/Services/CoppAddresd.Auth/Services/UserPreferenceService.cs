using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

public class UserPreferenceService : IUserPreferenceService
{
    private readonly AuthDbContext _dbContext;

    public UserPreferenceService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreferenceResponse?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await _dbContext.UserPreferences.FindAsync([userId], ct);
        return pref is null ? null : new UserPreferenceResponse(pref.Lang);
    }

    public async Task UpsertAsync(Guid userId, string lang, CancellationToken ct = default)
    {
        var existing = await _dbContext.UserPreferences.FindAsync([userId], ct);
        if (existing is null)
        {
            _dbContext.UserPreferences.Add(new UserPreference
            {
                UserId = userId,
                Lang = lang,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Lang = lang;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
