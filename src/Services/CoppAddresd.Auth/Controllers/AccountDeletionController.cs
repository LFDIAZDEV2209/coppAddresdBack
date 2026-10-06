using System.Runtime.CompilerServices;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Backend de la web de eliminación de cuenta (patrón BFF). La sesión vive en una
/// cookie HttpOnly + SameSite=Strict de alcance limitado a esta ruta: el navegador
/// nunca recibe un token. Toda acción exige un Origin de la lista permitida.
/// </summary>
[ApiController]
[Route("api/auth/account/deletion-session")]
[EnableRateLimiting("auth")]
public class AccountDeletionController : ControllerBase
{
    public const string SessionCookieName = "copp_account_deletion";
    private const string CookiePath = "/api/auth/account/deletion-session";
    private const string ConfirmationWord = "ELIMINAR";

    private readonly IAccountDeletionSessionService _sessions;
    private readonly AccountDeletionSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AccountDeletionController> _logger;

    public AccountDeletionController(
        IAccountDeletionSessionService sessions,
        IOptions<AccountDeletionSettings> settings,
        IHostEnvironment environment,
        ILogger<AccountDeletionController> logger
    )
    {
        _sessions = sessions;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Canjea el código de un solo uso que entrega la app por la cookie de sesión.</summary>
    [HttpPost]
    public async Task<ActionResult<DeletionSessionSummary>> StartFromCode(
        [FromBody] StartDeletionSessionFromCodeRequest request,
        CancellationToken ct
    )
    {
        if (!IsAllowedOrigin(requireHeader: true))
            return ForbiddenOrigin();
        var started = await _sessions.StartFromHandoffAsync(request.Code, ct);
        return started is null
            ? Unauthorized(new { message = "Código inválido o vencido" })
            : Started(started.Value);
    }

    /// <summary>Alternativa sin la app: documento + contraseña.</summary>
    [HttpPost("password")]
    public async Task<ActionResult<DeletionSessionSummary>> StartWithPassword(
        [FromBody] StartDeletionSessionWithPasswordRequest request,
        CancellationToken ct
    )
    {
        if (!IsAllowedOrigin(requireHeader: true))
            return ForbiddenOrigin();
        var started = await _sessions.StartWithPasswordAsync(
            request.DocumentNumber,
            request.Password,
            ct
        );
        return started is null
            ? Unauthorized(new { message = "Credenciales inválidas" })
            : Started(started.Value);
    }

    /// <summary>Alternativa sin la app ni contraseña: documento + código OTP.</summary>
    [HttpPost("otp")]
    public async Task<ActionResult<DeletionSessionSummary>> StartWithOtp(
        [FromBody] StartDeletionSessionWithOtpRequest request,
        CancellationToken ct
    )
    {
        if (!IsAllowedOrigin(requireHeader: true))
            return ForbiddenOrigin();
        var started = await _sessions.StartWithOtpAsync(request.DocumentNumber, request.Otp, ct);
        return started is null
            ? Unauthorized(new { message = "Código inválido o expirado" })
            : Started(started.Value);
    }

    /// <summary>Datos de la sesión vigente (p. ej. al recargar la página).</summary>
    [HttpGet]
    public async Task<ActionResult<DeletionSessionSummary>> Get(CancellationToken ct)
    {
        if (!IsAllowedOrigin(requireHeader: false))
            return ForbiddenOrigin();
        NoStore();
        var secret = Request.Cookies[SessionCookieName];
        var summary = secret is null ? null : await _sessions.GetAsync(secret, ct);
        return summary is null ? Unauthorized(new { message = "Sesión no válida" }) : Ok(summary);
    }

    /// <summary>Confirma y solicita la eliminación. Consume la sesión.</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<DeletionSessionConfirmed>> Confirm(
        [FromBody] ConfirmDeletionSessionRequest request,
        CancellationToken ct
    )
    {
        if (!IsAllowedOrigin(requireHeader: true))
            return ForbiddenOrigin();
        NoStore();
        if (
            !string.Equals(
                request.Confirmation.Trim(),
                ConfirmationWord,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return BadRequest(new { message = "Confirmación inválida" });
        }

        var secret = Request.Cookies[SessionCookieName];
        if (secret is null)
        {
            return Unauthorized(new { message = "Sesión no válida" });
        }

        var (success, error, result) = await _sessions.ConfirmAsync(secret, ct);
        ClearCookie();
        if (!success)
        {
            return error is null
                ? Unauthorized(new { message = "Sesión no válida" })
                : BadRequest(new { message = error });
        }

        return Ok(result);
    }

    /// <summary>Cancela la sesión sin eliminar nada.</summary>
    [HttpDelete]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        if (!IsAllowedOrigin(requireHeader: true))
            return ForbiddenOrigin();
        var secret = Request.Cookies[SessionCookieName];
        if (secret is not null)
        {
            await _sessions.CancelAsync(secret, ct);
        }

        ClearCookie();
        return NoContent();
    }

    private ActionResult<DeletionSessionSummary> Started(
        (string Secret, DeletionSessionSummary Summary) started
    )
    {
        NoStore();
        Response.Cookies.Append(
            SessionCookieName,
            started.Secret,
            CookieOptions(started.Summary.ExpiresAt)
        );
        return Ok(started.Summary);
    }

    /// <summary>
    /// Defensa CSRF adicional a SameSite=Strict: el Origin debe estar en la lista.
    /// Sin lista configurada el flujo queda deshabilitado (falla cerrado). Solo las
    /// lecturas aceptan peticiones same-origin sin cabecera Origin.
    /// </summary>
    private bool IsAllowedOrigin(bool requireHeader, [CallerMemberName] string action = "")
    {
        if (_settings.AllowedOrigins.Length == 0)
        {
            _logger.LogWarning(
                "Account deletion {Action} rejected: no AllowedOrigins configured (flujo deshabilitado)",
                action
            );
            return false;
        }

        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            if (requireHeader)
            {
                _logger.LogWarning(
                    "Account deletion {Action} rejected: POST sin cabecera Origin",
                    action
                );
                return false;
            }

            // Sin Origin (GET same-origin): Chromium manda Sec-Fetch-Site; Safari y
            // Firefox no lo implementan, asi que el mismo sitio se confirma con Referer.
            if (Request.Headers["Sec-Fetch-Site"].ToString() == "same-origin")
            {
                return true;
            }

            var referer = Request.Headers.Referer.ToString();
            if (
                !string.IsNullOrEmpty(referer)
                && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                && Matches($"{uri.Scheme}://{uri.Authority}")
            )
            {
                return true;
            }

            _logger.LogWarning(
                "Account deletion {Action} rejected: sin Origin, sin Sec-Fetch-Site same-origin y Referer no permitido (Referer={Referer})",
                action,
                referer
            );
            return false;
        }

        if (Matches(origin))
        {
            return true;
        }

        _logger.LogWarning(
            "Account deletion {Action} rejected: origin not allowed (Origin={Origin}, permitidos={Allowed})",
            action,
            origin,
            string.Join(", ", _settings.AllowedOrigins)
        );
        return false;
    }

    private bool Matches(string origin) =>
        _settings.AllowedOrigins.Any(o =>
            string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)
        );

    private ObjectResult ForbiddenOrigin([CallerMemberName] string action = "")
    {
        _logger.LogWarning(
            "Account deletion {Action}: respondiendo 403 por origen no permitido (Origin={Origin})",
            action,
            Request.Headers.Origin.ToString()
        );
        return StatusCode(StatusCodes.Status403Forbidden, new { message = "Origen no permitido" });
    }

    private void NoStore() => Response.Headers.CacheControl = "no-store";

    private void ClearCookie() =>
        Response.Cookies.Append(
            SessionCookieName,
            string.Empty,
            CookieOptions(DateTime.UtcNow.AddDays(-1))
        );

    private CookieOptions CookieOptions(DateTime expiresUtc) =>
        new()
        {
            HttpOnly = true,
            // En desarrollo la web va por http a través del proxy de Vite.
            Secure = !_environment.IsDevelopment(),
            // www.coppadresd.com → erp.coppadresd.com es mismo sitio: Strict basta y
            // bloquea el envío de la cookie desde cualquier otro sitio.
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            IsEssential = true,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc)),
        };
}
