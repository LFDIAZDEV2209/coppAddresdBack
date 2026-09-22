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
    private readonly IOtpService _otpService;
    private readonly IHostEnvironment _environment;

    public AuthController(IAuthService authService, IOtpService otpService, IHostEnvironment environment)
    {
        _authService = authService;
        _otpService = otpService;
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
            return Unauthorized(new { message = "Credenciales invÃ¡lidas" });
        }

        SetRefreshTokenCookie(result.RefreshToken, request.RememberMe);

        return Ok(new LoginResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    /// <summary>
    /// Primer inicio de sesiÃ³n por nÃºmero de identificaciÃ³n: devuelve los
    /// correos y telÃ©fonos asociados al ID (enmascarados) para que el usuario
    /// elija por dÃ³nde recibe el cÃ³digo OTP.
    /// </summary>
    [HttpPost("id-lookup")]
    public async Task<ActionResult<IdLookupResponse>> IdLookup(
        [FromBody] IdLookupRequest request,
        CancellationToken ct)
    {
        var result = await _otpService.LookupByIdAsync(request, ct);

        if (result is null)
        {
            return NotFound(new { message = "El nÃºmero de identificaciÃ³n no estÃ¡ registrado" });
        }

        return Ok(result);
    }

    /// <summary>
    /// EnvÃ­a el cÃ³digo OTP al mÃ©todo de contacto elegido. En desarrollo la
    /// respuesta incluye <c>devCode</c> para pruebas end-to-end.
    /// </summary>
    [HttpPost("send-otp")]
    public async Task<ActionResult<SendOtpResponse>> SendOtp(
        [FromBody] SendOtpRequest request,
        CancellationToken ct)
    {
        var (success, error, result) = await _otpService.SendOtpAsync(request, ct);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    /// <summary>
    /// Verifica el OTP y completa el primer inicio de sesiÃ³n: aprovisiona la
    /// cuenta (si no existe), la vincula al perfil del paciente, otorga acceso
    /// a la aplicaciÃ³n y emite los tokens de sesiÃ³n (refresh en cookie HttpOnly).
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<LoginResponse>> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        var result = await _otpService.VerifyOtpAsync(request, ct);

        if (result is null)
        {
            return Unauthorized(new { message = "CÃ³digo invÃ¡lido o expirado" });
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
            // Sin cookie: visitante que nunca tuvo sesiÃ³n. Se informa al
            // cliente para que NO muestre el banner de "sesiÃ³n expirada".
            Response.Headers["X-Refresh-Status"] = "missing";
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = "Refresh token invÃ¡lido o expirado" });
        }

        var result = await _authService.RefreshAsync(refreshToken, ct);

        if (result is null)
        {
            // Cookie corrupta, expirada o revocada: se limpia para que el
            // cliente se recupere sin intervenciÃ³n manual del usuario.
            Response.Headers["X-Refresh-Status"] = "invalid";
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = "Refresh token invÃ¡lido o expirado" });
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

        return Ok(new { message = "SesiÃ³n cerrada correctamente" });
    }


    /// <summary>
    /// Define la primera contrasena de una cuenta OTP (APP movil). Falla si la
    /// cuenta ya tiene contrasena: en ese caso usar change-password.
    /// </summary>
    [HttpPost("set-first-password")]
    [Authorize]
    public async Task<IActionResult> SetFirstPassword(
        [FromBody] SetFirstPasswordRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { message = "Token invalido" });
        }

        var (success, error) = await _authService.SetFirstPasswordAsync(userId, new ChangePasswordRequest
        {
            NewPassword = request.NewPassword
        }, ct);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { message = "Contrasena establecida" });
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
            return Unauthorized(new { message = "Token invÃ¡lido" });
        }

        var (success, error) = await _authService.ChangePasswordAsync(userId, request, ct);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        ClearRefreshTokenCookie();

        return Ok(new { message = "ContraseÃ±a cambiada correctamente. Debe iniciar sesiÃ³n nuevamente." });
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
            // Secure solo en contextos seguros; LAN/dev usan http. En producciÃ³n
            // el servicio se expone siempre por HTTPS.
            Secure = !_environment.IsDevelopment(),
            // En producciÃ³n el frontend y la API viven en orÃ­genes distintos
            // (frontend â†’ API Gateway), por lo que la cookie de refresh se envÃ­a
            // en requests cross-site: SameSite=None es obligatorio. Lax en dev
            // (mismo sitio localhost) evita el aviso del navegador.
            SameSite = _environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
            Path = "/api/auth",
            IsEssential = true,
            Expires = expires
        };
    }
}
/// <summary>Solicita la primera contrasena de una cuenta OTP.</summary>
public sealed record SetFirstPasswordRequest(string NewPassword);