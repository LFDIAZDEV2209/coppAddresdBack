using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CoppAddresd.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Security;

public class SecurityStampValidator : ISecurityStampValidator
{
    private readonly UserManager<Entities.ApplicationUser> _userManager;
    private readonly ILogger<SecurityStampValidator> _logger;
    private readonly AuthDbContext _db;

    public SecurityStampValidator(
        UserManager<Entities.ApplicationUser> userManager,
        ILogger<SecurityStampValidator> logger,
        AuthDbContext db)
    {
        _userManager = userManager;
        _logger = logger;
        _db = db;
    }

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct = default)
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

        var access = await _db.UserApplications.AsNoTracking()
            .Where(x => x.UserId == userId && x.Application.Code == "erp")
            .Select(x => new { x.IsSuspended, x.SessionVersion, x.Application.IsActive })
            .SingleOrDefaultAsync(ct);
        return access is not null && access.IsActive && !access.IsSuspended
            && ErpSessionVersion.Matches(principal.FindFirst("application_session_version")?.Value, access.SessionVersion);
    }
}

public interface ISecurityStampValidator
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}

/// <summary>Compatibilidad de tokens previos: sin claim equivale a versión cero, nunca a una posterior.</summary>
public static class ErpSessionVersion
{
    public static bool Matches(string? claim, long current) =>
        claim is null ? current == 0 :
        long.TryParse(claim, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var version) && version == current;
}
