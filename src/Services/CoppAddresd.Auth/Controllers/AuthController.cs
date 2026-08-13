using System.Security.Claims;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoppAddresd.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "copp_refresh_token";
    private const int RefreshTokenMaxAgeDays = 7;
    private const int SessionCookieMaxAgeHours = 8;

    private readonly IAuthService _authService;
    private readonly IHostEnvironment _environment;

    public AuthController(IAuthService authService, IHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);

        if (result is null)
        {
            return Unauthorized(new { message = "Credenciales inválidas" });
        }

        SetRefreshTokenCookie(result.RefreshToken, request.RememberMe);

        return Ok(new LoginResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName]
            ?? request?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // Sin cookie: visitante que nunca tuvo sesión. Se informa al
            // cliente para que NO muestre el banner de "sesión expirada".
            Response.Headers["X-Refresh-Status"] = "missing";
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        var result = await _authService.RefreshAsync(refreshToken, ct);

        if (result is null)
        {
            // Cookie corrupta, expirada o revocada: se limpia para que el
            // cliente se recupere sin intervención manual del usuario.
            Response.Headers["X-Refresh-Status"] = "invalid";
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        SetRefreshTokenCookie(result.RefreshToken, rememberMe: true);

        return Ok(new RefreshTokenResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // El logout se resuelve con la cookie de refresh, sin depender del
        // access token (que pudo haber expirado). Sin cookie: idempotente.
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var userId = await _authService.GetUserIdByRefreshTokenAsync(refreshToken, ct);
            if (userId.HasValue)
            {
                await _authService.LogoutAsync(userId.Value, ct);
            }
        }

        ClearRefreshTokenCookie();

        return Ok(new { message = "Sesión cerrada correctamente" });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { message = "Token inválido" });
        }

        var (success, error) = await _authService.ChangePasswordAsync(userId, request, ct);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        ClearRefreshTokenCookie();

        return Ok(new { message = "Contraseña cambiada correctamente. Debe iniciar sesión nuevamente." });
    }

    private void SetRefreshTokenCookie(string refreshToken, bool rememberMe)
    {
        var expires = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(RefreshTokenMaxAgeDays)
            : DateTimeOffset.UtcNow.AddHours(SessionCookieMaxAgeHours);

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildCookieOptions(expires));
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Append(RefreshTokenCookieName, string.Empty,
            BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            // En desarrollo (http://localhost) los navegadores aceptan cookies
            // Secure solo en contextos seguros; LAN/dev usan http. En producción
            // el servicio se expone siempre por HTTPS.
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            IsEssential = true,
            Expires = expires
        };
    }
}