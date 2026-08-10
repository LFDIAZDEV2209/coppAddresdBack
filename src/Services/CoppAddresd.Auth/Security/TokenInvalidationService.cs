using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;

namespace CoppAddresd.Auth.Security;

public class TokenInvalidationService : ITokenInvalidationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TokenInvalidationService> _logger;

    public TokenInvalidationService(
        UserManager<ApplicationUser> userManager,
        ILogger<TokenInvalidationService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Cannot invalidate tokens: user {UserId} not found", userId);
            return;
        }

        await _userManager.UpdateSecurityStampAsync(user);
        _logger.LogInformation("Security stamp updated for user {UserId}, all tokens invalidated", userId);
    }
}
