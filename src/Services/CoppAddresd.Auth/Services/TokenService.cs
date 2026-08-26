using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoppAddresd.Auth.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly AuthDbContext _dbContext;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        IOptions<JwtSettings> jwtSettings,
        AuthDbContext dbContext,
        ILogger<TokenService> logger)
    {
        _jwtSettings = jwtSettings.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    public string GenerateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        string audience,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new("security_stamp", user.SecurityStamp ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Cada código de permiso viaja como claim propio: el PermissionHandler
        // autoriza leyendo estos claims sin consultar la BD por request.
        var permissionList = permissions as IReadOnlyCollection<string> ?? permissions.ToList();
        foreach (var permissionCode in permissionList)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, permissionCode));

            // Transición dual-emit: un grant de un código legado Telemedicine.*
            // también emite su equivalente nuevo Appointments.*. Así los tokens
            // emitidos durante la transición llevan AMBOS códigos y el consumidor
            // puede migrar a Appointments.* sin esperar a que se reasignen los
            // grants. No se duplica si el usuario ya tiene el código nuevo.
            if (PermissionCodeMap.TryGetNewCode(permissionCode, out var newCode)
                && !permissionList.Contains(newCode))
            {
                claims.Add(new Claim(PermissionClaimTypes.Permission, newCode));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            // El audience es el código de la aplicación con la que el usuario
            // se autentica ("erp", "app"): la validación en cada API exige que
            // el `aud` del token esté dentro de las audiencias permitidas.
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        _logger.LogDebug("Generated access token for user {UserId} with audience {Audience}", user.Id, audience);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, Guid? applicationId, CancellationToken ct = default)
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var tokenString = Convert.ToBase64String(randomNumber);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            ApplicationId = applicationId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Generated refresh token for user {UserId}", userId);

        return tokenString;
    }
}
