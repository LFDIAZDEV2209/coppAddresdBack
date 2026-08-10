using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Security;

public class SecurityStampValidator : ISecurityStampValidator
{
    private readonly UserManager<Entities.ApplicationUser> _userManager;
    private readonly ILogger<SecurityStampValidator> _logger;

    public SecurityStampValidator(
        UserManager<Entities.ApplicationUser> userManager,
        ILogger<SecurityStampValidator> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return false;
        }

        var securityStampClaim = principal.FindFirst("security_stamp");
        if (securityStampClaim is null)
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Security stamp validation failed: user {UserId} not found", userId);
            return false;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Security stamp validation failed: user {UserId} is disabled", userId);
            return false;
        }

        if (user.SecurityStamp != securityStampClaim.Value)
        {
            _logger.LogWarning("Security stamp validation failed: stamp mismatch for user {UserId}", userId);
            return false;
        }

        return true;
    }
}

public interface ISecurityStampValidator
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal);
}
