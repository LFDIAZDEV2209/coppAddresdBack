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

    public async Task<UserPreferenceResponse?> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var pref = await _dbContext.UserPreferences.FindAsync([userId], ct);
        return pref is null ? null : new UserPreferenceResponse(pref.Lang, pref.AccentColor);
    }

    public async Task UpsertAsync(
        Guid userId,
        string? lang,
        string? accentColor,
        CancellationToken ct = default
    )
    {
        var existing = await _dbContext.UserPreferences.FindAsync([userId], ct);
        if (existing is null)
        {
            _dbContext.UserPreferences.Add(
                new UserPreference
                {
                    UserId = userId,
                    Lang = lang,
                    AccentColor = accentColor,
                    UpdatedAt = DateTime.UtcNow,
                }
            );
        }
        else
        {
            // Actualización parcial: solo los campos presentes cambian.
            if (lang is not null)
                existing.Lang = lang;
            if (accentColor is not null)
                existing.AccentColor = accentColor;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
