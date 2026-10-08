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
            return Unauthorized(new { message = "Credenciales inválidas" });
        }

        SetRefreshTokenCookie(result.RefreshToken, request.RememberMe, request.Application);

        return Ok(new LoginResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    /// <summary>
    /// Primer inicio de sesión por número de identificación: devuelve los
    /// correos y teléfonos asociados al ID (enmascarados) para que el usuario
    /// elija por dónde recibe el código OTP.
    /// </summary>
    [HttpPost("id-lookup")]
    public async Task<ActionResult<IdLookupResponse>> IdLookup(
        [FromBody] IdLookupRequest request,
        CancellationToken ct)
    {
        var result = await _otpService.LookupByIdAsync(request, ct);

        if (result is null)
        {
            return NotFound(new { message = "El número de identificación no está registrado" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Envía el código OTP al método de contacto elegido. En desarrollo la
    /// respuesta incluye <c>devCode</c> para pruebas end-to-end.
    /// </summary>
    [HttpPost("recovery-code")]
    [AllowAnonymous]
    public async Task<IActionResult> SendRecoveryCode([FromBody] PasswordRecoveryCodeRequest request,
        [FromServices] CoppAddresd.Auth.Services.PasswordRecoveryService recovery, CancellationToken ct)
        => Ok(await recovery.SendAsync(request, ct));

    [HttpPost("recover-password")]
    [AllowAnonymous]
    public async Task<IActionResult> RecoverPassword([FromBody] PasswordRecoveryRequest request,
        [FromServices] CoppAddresd.Auth.Services.PasswordRecoveryService recovery, CancellationToken ct)
    {
        if (!await recovery.ResetAsync(request, ct)) return BadRequest(new { message = "Código inválido o expirado." });
        ClearRefreshTokenCookie("app");
        return Ok(new { message = "Contraseña actualizada. Inicia sesión con tu nueva contraseña." });
    }

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
    /// Verifica el OTP y completa el primer inicio de sesión: aprovisiona la
    /// cuenta (si no existe), la vincula al perfil del paciente, otorga acceso
    /// a la aplicación y emite los tokens de sesión (refresh en cookie HttpOnly).
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<LoginResponse>> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        var result = await _otpService.VerifyOtpAsync(request, ct);

        if (result is null)
        {
            return Unauthorized(new { message = "Código inválido o expirado" });
        }

        SetRefreshTokenCookie(result.RefreshToken, request.RememberMe, request.Application);

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
        var application = (request?.Application ?? "erp").ToLowerInvariant();
        // Migración: la cookie antigua solo se acepta si el servicio verifica su aplicación.
        var refreshToken = Request.Cookies[CookieName(application)]
            ?? Request.Cookies[RefreshTokenCookieName]
            ?? request?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // Sin cookie: visitante que nunca tuvo sesión. Se informa al
            // cliente para que NO muestre el banner de "sesión expirada".
            Response.Headers["X-Refresh-Status"] = "missing";
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        var result = await _authService.RefreshAsync(refreshToken, ct, application);

        if (result is null)
        {
            // No borra cookies aquí: otra pestaña puede haber completado una rotación.
            Response.Headers["X-Refresh-Status"] = "invalid";
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        SetRefreshTokenCookie(result.RefreshToken, rememberMe: true, application);

        return Ok(new RefreshTokenResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        // El logout se resuelve con la cookie de refresh, sin depender del
        // access token (que pudo haber expirado). Sin cookie: idempotente.
        var application = (request?.Application ?? "erp").ToLowerInvariant();
        var refreshToken = Request.Cookies[CookieName(application)] ?? Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var userId = await _authService.GetUserIdByRefreshTokenAsync(refreshToken, ct, application);
            if (userId.HasValue)
            {
                await _authService.LogoutAsync(userId.Value, ct, application);
                Response.Cookies.Delete(RefreshTokenCookieName, BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)));
            }
        }

        ClearRefreshTokenCookie(application);

        return Ok(new { message = "Sesión cerrada correctamente" });
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
            return Unauthorized(new { message = "Token inválido" });
        }

        var (success, error) = await _authService.SetFirstPasswordAsync(userId, new ChangePasswordRequest
        {
            NewPassword = request.NewPassword
        }, ct);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { message = "Contraseña establecida" });
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

        ClearRefreshTokenCookie("erp");
        ClearRefreshTokenCookie("app");

        return Ok(new { message = "Contraseña cambiada correctamente. Debe iniciar sesión nuevamente." });
    }

    private static string CookieName(string application) => $"{RefreshTokenCookieName}_{application.ToLowerInvariant()}";

    private void SetRefreshTokenCookie(string refreshToken, bool rememberMe, string application)
    {
        var expires = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(RefreshTokenMaxAgeDays)
            : DateTimeOffset.UtcNow.AddHours(SessionCookieMaxAgeHours);

        Response.Cookies.Append(CookieName(application), refreshToken, BuildCookieOptions(expires));
        Response.Cookies.Delete(RefreshTokenCookieName, BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    private void ClearRefreshTokenCookie(string application)
    {
        Response.Cookies.Append(CookieName(application), string.Empty,
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
            // En producción el frontend y la API viven en orígenes distintos
            // (frontend → API Gateway), por lo que la cookie de refresh se envía
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