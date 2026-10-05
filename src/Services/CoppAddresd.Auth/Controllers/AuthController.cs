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
    // Cookie de refresh por aplicacion: copp_refresh_token_{erp|app}. ERP y app
    // del paciente comparten host en el navegador (las cookies no distinguen
    // puertos), asi que cada aplicacion escribe y lee la suya. La cookie sin
    // sufijo es la de los clientes anteriores: solo se lee como respaldo de
    // transicion y nunca se vuelve a escribir cuando el cliente envia application.
    public const string LegacyRefreshTokenCookieName = "copp_refresh_token";
    public const string RefreshTokenCookiePrefix = LegacyRefreshTokenCookieName + "_";
    private const int MaxApplicationCodeLength = 32;
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
        var application = NormalizeApplication(request?.Application);
        var (refreshToken, fromLegacyCookie) = ResolveRefreshToken(application, request?.RefreshToken);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // Sin cookie: visitante que nunca tuvo sesión. Se informa al
            // cliente para que NO muestre el banner de "sesión expirada".
            Response.Headers["X-Refresh-Status"] = "missing";
            ClearRefreshCookies(application, includeLegacy: application is null);
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        // Un token emitido para otra aplicacion (p. ej. la cookie heredada del
        // ERP llegando a la app del paciente) no es de este cliente: se trata
        // como ausente, SIN rotarlo ni limpiar cookies ajenas.
        if (application is not null && await BelongsToOtherApplicationAsync(refreshToken, application, ct))
        {
            Response.Headers["X-Refresh-Status"] = "missing";
            return Unauthorized(new { message = "Refresh token no corresponde a la aplicacion" });
        }

        var result = await _authService.RefreshAsync(refreshToken, ct);

        if (result is null)
        {
            // Cookie corrupta, expirada o revocada: se limpia para que el
            // cliente se recupere sin intervención manual del usuario.
            Response.Headers["X-Refresh-Status"] = "invalid";
            ClearRefreshCookies(application, includeLegacy: application is null || fromLegacyCookie);
            return Unauthorized(new { message = "Refresh token inválido o expirado" });
        }

        SetRefreshTokenCookie(result.RefreshToken, rememberMe: true, application);

        // Migracion: la sesion heredada de la cookie sin sufijo pasa a la propia
        // de la aplicacion y la antigua se retira.
        if (application is not null && fromLegacyCookie)
        {
            ClearRefreshCookies(application: null, includeLegacy: true);
        }

        return Ok(new RefreshTokenResponse(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken ct)
    {
        // El logout se resuelve con la cookie de refresh, sin depender del
        // access token (que pudo haber expirado). Sin cookie: idempotente.
        var application = NormalizeApplication(request?.Application);
        var (refreshToken, fromLegacyCookie) = ResolveRefreshToken(application, bodyToken: null);
        var clearLegacy = application is null;

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            // La cookie heredada puede ser de la otra aplicacion: ni se revoca
            // ni se retira (seguiria siendo la sesion valida de esa app).
            var otherApplication = application is not null
                && await BelongsToOtherApplicationAsync(refreshToken, application, ct);

            if (!otherApplication)
            {
                clearLegacy = clearLegacy || fromLegacyCookie;
                var userId = await _authService.GetUserIdByRefreshTokenAsync(refreshToken, ct);
                if (userId.HasValue)
                {
                    await _authService.LogoutAsync(userId.Value, ct);
                }
            }
        }

        ClearRefreshCookies(application, clearLegacy);

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

        // Todos los refresh tokens del usuario quedaron revocados: se retiran la
        // cookie heredada y la de la aplicacion con la que llego este access token.
        ClearRefreshCookies(NormalizeApplication(User.FindFirst("aud")?.Value), includeLegacy: true);

        return Ok(new { message = "Contraseña cambiada correctamente. Debe iniciar sesión nuevamente." });
    }

    /// <summary>
    /// Nombre de la cookie de refresh de una aplicacion (<c>copp_refresh_token_erp</c>).
    /// Sin aplicacion valida devuelve el nombre heredado, que es lo que siguen
    /// usando los clientes anteriores a la cookie por aplicacion.
    /// </summary>
    public static string RefreshCookieName(string? application)
    {
        var code = NormalizeApplication(application);
        return code is null ? LegacyRefreshTokenCookieName : RefreshTokenCookiePrefix + code;
    }

    /// <summary>
    /// Normaliza el codigo de aplicacion a minusculas y solo acepta
    /// <c>[a-z0-9_-]</c> (maximo 32): el valor termina en el nombre de una
    /// cookie, asi que cualquier otra cosa se descarta en lugar de escaparse.
    /// </summary>
    private static string? NormalizeApplication(string? application)
    {
        if (string.IsNullOrWhiteSpace(application))
        {
            return null;
        }

        var code = application.Trim().ToLowerInvariant();
        if (code.Length > MaxApplicationCodeLength)
        {
            return null;
        }

        foreach (var c in code)
        {
            var valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!valid)
            {
                return null;
            }
        }

        return code;
    }

    /// <summary>
    /// Origen del refresh token: cookie de la aplicacion, cookie heredada
    /// (solo como respaldo) o cuerpo de la peticion.
    /// </summary>
    private (string? Token, bool FromLegacyCookie) ResolveRefreshToken(string? application, string? bodyToken)
    {
        if (application is not null)
        {
            var named = Request.Cookies[RefreshCookieName(application)];
            if (!string.IsNullOrWhiteSpace(named))
            {
                return (named, false);
            }
        }

        var legacy = Request.Cookies[LegacyRefreshTokenCookieName];
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            return (legacy, true);
        }

        return (string.IsNullOrWhiteSpace(bodyToken) ? null : bodyToken, false);
    }

    /// <summary>
    /// True si el token existe, esta activo y fue emitido para otra aplicacion.
    /// Un token inexistente o inactivo devuelve false: lo resuelve el flujo normal.
    /// </summary>
    private async Task<bool> BelongsToOtherApplicationAsync(
        string refreshToken, string application, CancellationToken ct)
    {
        var tokenApplication = await _authService.GetRefreshTokenApplicationCodeAsync(refreshToken, ct);
        return tokenApplication is not null
            && !string.Equals(tokenApplication, application, StringComparison.OrdinalIgnoreCase);
    }

    private void SetRefreshTokenCookie(string refreshToken, bool rememberMe, string? application)
    {
        var expires = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(RefreshTokenMaxAgeDays)
            : DateTimeOffset.UtcNow.AddHours(SessionCookieMaxAgeHours);

        Response.Cookies.Append(RefreshCookieName(application), refreshToken, BuildCookieOptions(expires));
    }

    /// <summary>
    /// Retira la cookie de la aplicacion (si hay) y, opcionalmente, la heredada.
    /// La heredada solo se retira cuando es seguro: nunca por una peticion de
    /// una aplicacion cuando esa cookie puede ser la sesion de la otra.
    /// </summary>
    private void ClearRefreshCookies(string? application, bool includeLegacy)
    {
        var expired = BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(-1));

        if (application is not null)
        {
            Response.Cookies.Append(RefreshCookieName(application), string.Empty, expired);
        }

        if (includeLegacy)
        {
            Response.Cookies.Append(LegacyRefreshTokenCookieName, string.Empty, expired);
        }
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