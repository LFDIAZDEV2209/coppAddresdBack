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
    private readonly IPermissionService _permissionService;
    private readonly AuthDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IPermissionService permissionService,
        AuthDbContext dbContext,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _permissionService = permissionService;
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Autentica a un usuario. Soporta dos modos de resolución de identidad:
    /// 1. Por correo (ERP): cuando <c>DocumentNumber</c> es nulo o vacío, el
    ///    usuario se resuelve con <see cref="UserManager{TUser}.FindByEmailAsync"/>.
    /// 2. Por número de documento (app móvil): cuando <c>DocumentNumber</c> se
    ///    informa, se busca primero el <c>user_id</c> en
    ///    <c>app.patient_profiles</c> y luego el usuario por su Id. Esto permite
    ///    al paciente iniciar sesión con su número de identificación en la
    ///    aplicación "app" sin conocer su correo.
    /// En ambos casos se conservan las validaciones existentes (activo,
    /// password, aplicación existente y acceso por UserApplication).
    /// </summary>
    public async Task<TokenResult?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ApplicationUser? user;

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            // Modo ERP: resolución por correo (comportamiento original).
            user = await _userManager.FindByEmailAsync(request.Email);

            if (user is null)
            {
                _logger.LogWarning("Login failed: user not found for email {Email}", request.Email);
                return null;
            }
        }
        else
        {
            // Modo app móvil: resolver el usuario a través del paciente.
            var patient = await _dbContext.Database
                .SqlQueryRaw<PatientUserIdRow>(
                    "SELECT \"user_id\" AS \"UserId\" FROM app.patient_profiles WHERE \"document_number\" = {0} AND \"deleted_at\" IS NULL LIMIT 1",
                    request.DocumentNumber)
                .FirstOrDefaultAsync(ct);

            if (patient is null || patient.UserId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Login failed: no patient profile linked to document number {DocumentNumber}",
                    request.DocumentNumber);
                return null;
            }

            user = await _userManager.FindByIdAsync(patient.UserId.ToString());

            if (user is null)
            {
                _logger.LogWarning(
                    "Login failed: user {UserId} not found for document number {DocumentNumber}",
                    patient.UserId, request.DocumentNumber);
                return null;
            }
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

        // La aplicación del login determina el `aud` del token. El acceso se
        // resuelve explícitamente por UserApplication: roles y permisos no
        // determinan a qué aplicaciones puede entrar el usuario.
        var application = await _dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == request.Application, ct);

        if (application is null || !application.IsActive)
        {
            _logger.LogWarning("Login failed: application {Application} not found or inactive for user {UserId}",
                request.Application, user.Id);
            return null;
        }

        var hasAccess = await _dbContext.UserApplications
            .AsNoTracking()
            .AnyAsync(ua => ua.UserId == user.Id && ua.ApplicationId == application.Id, ct);

        if (!hasAccess)
        {
            _logger.LogWarning("Login failed: user {UserId} has no access to application {Application}",
                user.Id, application.Code);
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        // Permisos actuales (directos + via rol) al momento del login: se emiten
        // como claims en el access token para que la autorización no consulte BD.
        var permissions = await _permissionService.GetUserAllPermissionCodesAsync(user.Id, ct);
        var accessToken = _tokenService.GenerateAccessToken(user, roles, application.Code, permissions);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, application.Id, ct);

        _logger.LogInformation("User {UserId} logged in successfully to application {Application}",
            user.Id, application.Code);

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
            .Include(rt => rt.Application)
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

        // El refresh conserva la aplicación con la que se emitió el token
        // original: el nuevo access token mantiene el mismo `aud`.
        if (storedToken.Application is null || !storedToken.Application.IsActive)
        {
            _logger.LogWarning("Refresh failed: token {TokenId} has no valid application binding", storedToken.Id);
            return null;
        }

        // Reclamación ATÓMICA del token: un solo UPDATE condicional revoca el
        // token SI y SOLO SI aún no fue usado (REQ-REFRESH-01). Dos solicitudes
        // concurrentes con el mismo token compiten por este UPDATE: solo una
        // gana (affected == 1); la perdedora (affected == 0) ve que el token ya
        // fue reclamado y se rechaza SIN emitir una nueva familia. Antes era un
        // check-then-act (IsActive leído antes de escribir) que permitía minting
        // múltiple ante replay concurrente de un token robado.
        var claimed = await _dbContext.RefreshTokens
            .Where(rt => rt.Token == refreshToken && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow),
                ct);

        if (claimed == 0)
        {
            _logger.LogWarning(
                "Refresh failed: token already claimed (concurrent rotation or replay) for user {UserId}",
                storedToken.UserId);
            return null;
        }

        // Espejo del tracker: el UPDATE batch no toca la entidad tracked; sin
        // esto, SaveChangesAsync reescribiría RevokedAt = null y revertiría la
        // reclamación. El valor escrito coincide con el del batch (idempotente).
        storedToken.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(
            storedToken.UserId, storedToken.ApplicationId, ct);
        storedToken.ReplacedByTokenId = (await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == newRefreshToken, ct))?.Id;

        await _dbContext.SaveChangesAsync(ct);

        var roles = await _userManager.GetRolesAsync(storedToken.User);
        // Re-cálculo de permisos en cada refresh: el nuevo access token refleja
        // el estado ACTUAL (no copia claims del token anterior).
        var permissions = await _permissionService.GetUserAllPermissionCodesAsync(storedToken.UserId, ct);
        var newAccessToken = _tokenService.GenerateAccessToken(storedToken.User, roles, storedToken.Application.Code, permissions);

        _logger.LogInformation("Refreshed tokens for user {UserId} (application {Application})",
            storedToken.UserId, storedToken.Application.Code);

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

    /// <summary>
    /// Fila de proyección para la consulta raw que resuelve el <c>user_id</c>
    /// vinculado a un número de documento en <c>app.patient_profiles</c>. El
    /// mapeo de columna se hace por nombre mediante el alias <c>AS "UserId"</c>.
    /// </summary>
    private sealed record PatientUserIdRow
    {
        public Guid UserId { get; set; }
    }
}
