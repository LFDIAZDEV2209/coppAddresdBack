using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly AuthDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        AuthDbContext dbContext,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<TokenResult?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        
        if (user is null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", request.Email);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {UserId} is disabled", user.Id);
            return null;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login failed: user {UserId} is locked out", user.Id);
            }
            else
            {
                _logger.LogWarning("Login failed: invalid password for user {UserId}", user.Id);
            }
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, ct);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new TokenResult(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: "Bearer",
            ExpiresIn: _jwtSettings.AccessTokenExpirationMinutes * 60);
    }

    public async Task<TokenResult?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (storedToken is null)
        {
            _logger.LogWarning("Refresh failed: token not found");
            return null;
        }

        if (!storedToken.IsActive)
        {
            _logger.LogWarning("Refresh failed: token is not active (expired or revoked) for user {UserId}", storedToken.UserId);
            return null;
        }

        if (!storedToken.User.IsActive)
        {
            _logger.LogWarning("Refresh failed: user {UserId} is disabled", storedToken.UserId);
            return null;
        }

        storedToken.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(storedToken.UserId, ct);
        storedToken.ReplacedByTokenId = (await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == newRefreshToken, ct))?.Id;

        await _dbContext.SaveChangesAsync(ct);

        var roles = await _userManager.GetRolesAsync(storedToken.User);
        var newAccessToken = _tokenService.GenerateAccessToken(storedToken.User, roles);

        _logger.LogInformation("Refreshed tokens for user {UserId}", storedToken.UserId);

        return new TokenResult(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: _jwtSettings.AccessTokenExpirationMinutes * 60);
    }

    public async Task<Guid?> GetUserIdByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (storedToken is null || !storedToken.IsActive)
        {
            return null;
        }

        return storedToken.UserId;
    }

    public async Task<bool> LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} logged out, revoked {Count} refresh tokens", userId, activeTokens.Count);

        return true;
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password change failed for user {UserId}: {Errors}", userId, errors);
            return (false, errors);
        }

        await _userManager.UpdateSecurityStampAsync(user);

        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Password changed for user {UserId}, all tokens invalidated", userId);

        return (true, null);
    }
}
