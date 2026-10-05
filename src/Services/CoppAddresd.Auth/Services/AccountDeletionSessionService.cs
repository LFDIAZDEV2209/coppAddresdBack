using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Sesión de la web de eliminación de cuenta (patrón BFF): el Auth Service guarda
/// el estado y el navegador solo recibe una cookie HttpOnly con un secreto opaco.
/// Ningún access/refresh token llega al JavaScript de la web.
/// </summary>
public interface IAccountDeletionSessionService
{
    /// <summary>Canjea (una vez) el código que entrega la app. Null si es inválido, usado o vencido.</summary>
    Task<(string Secret, DeletionSessionSummary Summary)?> StartFromHandoffAsync(string code, CancellationToken ct = default);

    /// <summary>Verifica documento + contraseña sin emitir sesión de app. Null si no es válido.</summary>
    Task<(string Secret, DeletionSessionSummary Summary)?> StartWithPasswordAsync(string documentNumber, string password, CancellationToken ct = default);

    /// <summary>Verifica documento + OTP sin emitir sesión de app. Null si no es válido.</summary>
    Task<(string Secret, DeletionSessionSummary Summary)?> StartWithOtpAsync(string documentNumber, string otp, CancellationToken ct = default);

    Task<DeletionSessionSummary?> GetAsync(string secret, CancellationToken ct = default);

    /// <summary>Consume la sesión y solicita la eliminación. Null si la sesión no es válida.</summary>
    Task<(bool Success, string? Error, DeletionSessionConfirmed? Result)> ConfirmAsync(string secret, CancellationToken ct = default);

    Task CancelAsync(string secret, CancellationToken ct = default);
}

public class AccountDeletionSessionService : IAccountDeletionSessionService
{
    private readonly AuthDbContext _db;
    private readonly IAuthService _authService;
    private readonly IOtpService _otpService;
    private readonly IEmailSender _emailSender;
    private readonly AccountDeletionSettings _settings;
    private readonly ILogger<AccountDeletionSessionService> _logger;

    public AccountDeletionSessionService(
        AuthDbContext db,
        IAuthService authService,
        IOtpService otpService,
        IEmailSender emailSender,
        IOptions<AccountDeletionSettings> settings,
        ILogger<AccountDeletionSessionService> logger)
    {
        _db = db;
        _authService = authService;
        _otpService = otpService;
        _emailSender = emailSender;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(string Secret, DeletionSessionSummary Summary)?> StartFromHandoffAsync(
        string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var hash = AccountDeletionSecrets.Hash(code);
        var now = DateTime.UtcNow;

        // Consumo atómico: solo una petición puede marcar el código como usado.
        var consumed = await _db.AccountDeletionHandoffs
            .Where(h => h.CodeHash == hash && h.UsedAt == null && h.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.UsedAt, now), ct);
        if (consumed != 1)
        {
            _logger.LogWarning("Account deletion handoff rejected: code invalid, used or expired");
            return null;
        }

        var handoff = await _db.AccountDeletionHandoffs.AsNoTracking()
            .SingleAsync(h => h.CodeHash == hash, ct);
        return await CreateSessionAsync(handoff.UserId, handoff.ApplicationId, ct);
    }

    public async Task<(string Secret, DeletionSessionSummary Summary)?> StartWithPasswordAsync(
        string documentNumber, string password, CancellationToken ct = default)
    {
        // Reutiliza la validación del login (bloqueo por intentos, usuario activo,
        // acceso a la aplicación) y descarta de inmediato la sesión de app emitida.
        var tokens = await _authService.LoginAsync(new LoginRequest
        {
            DocumentNumber = documentNumber.Trim(),
            Password = password,
            Application = _settings.Application,
            RememberMe = false,
        }, ct);
        return tokens is null ? null : await StartFromIssuedTokensAsync(tokens, ct);
    }

    public async Task<(string Secret, DeletionSessionSummary Summary)?> StartWithOtpAsync(
        string documentNumber, string otp, CancellationToken ct = default)
    {
        var tokens = await _otpService.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = documentNumber.Trim(),
            Otp = otp.Trim(),
            Application = _settings.Application,
            RememberMe = false,
        }, ct);
        return tokens is null ? null : await StartFromIssuedTokensAsync(tokens, ct);
    }

    public async Task<DeletionSessionSummary?> GetAsync(string secret, CancellationToken ct = default)
    {
        var session = await FindActiveAsync(secret, ct);
        return session is null ? null : await SummarizeAsync(session, ct);
    }

    public async Task<(bool Success, string? Error, DeletionSessionConfirmed? Result)> ConfirmAsync(
        string secret, CancellationToken ct = default)
    {
        var hash = AccountDeletionSecrets.Hash(secret);
        var now = DateTime.UtcNow;

        // La sesión solo sirve una vez: se consume antes de actuar.
        var consumed = await _db.AccountDeletionSessions
            .Where(s => s.SecretHash == hash && s.UsedAt == null && s.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, now), ct);
        if (consumed != 1)
        {
            return (false, null, null);
        }

        var session = await _db.AccountDeletionSessions.AsNoTracking()
            .SingleAsync(s => s.SecretHash == hash, ct);
        var application = await _db.Applications.AsNoTracking()
            .SingleAsync(a => a.Id == session.ApplicationId, ct);

        var (success, error, purgeAfter) =
            await _authService.RequestAccountDeletionAsync(session.UserId, application.Code, ct);
        if (!success)
        {
            return (false, error, null);
        }

        await NotifyAsync(session.UserId, purgeAfter, ct);
        return (true, null, new DeletionSessionConfirmed(purgeAfter, AuthService.AccountRetentionDays));
    }

    public async Task CancelAsync(string secret, CancellationToken ct = default)
    {
        var hash = AccountDeletionSecrets.Hash(secret);
        var now = DateTime.UtcNow;
        await _db.AccountDeletionSessions
            .Where(s => s.SecretHash == hash && s.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, now), ct);
    }

    private async Task<(string Secret, DeletionSessionSummary Summary)?> StartFromIssuedTokensAsync(
        TokenResult tokens, CancellationToken ct)
    {
        // El login/OTP emite una sesión de app que aquí no se usa: se revoca para
        // que no quede un refresh token vigente fuera del dispositivo del usuario.
        var refresh = await _db.RefreshTokens.SingleOrDefaultAsync(rt => rt.Token == tokens.RefreshToken, ct);
        if (refresh is null || refresh.ApplicationId is null)
        {
            return null;
        }

        refresh.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await CreateSessionAsync(refresh.UserId, refresh.ApplicationId.Value, ct);
    }

    private async Task<(string Secret, DeletionSessionSummary Summary)?> CreateSessionAsync(
        Guid userId, Guid applicationId, CancellationToken ct)
    {
        var access = await _db.UserApplications
            .Include(ua => ua.User)
            .Include(ua => ua.Application)
            .AsNoTracking()
            .SingleOrDefaultAsync(ua => ua.UserId == userId && ua.ApplicationId == applicationId, ct);
        if (access is null || access.IsSuspended || !access.User.IsActive || !access.Application.IsActive
            || !string.Equals(access.Application.Code, _settings.Application, StringComparison.Ordinal))
        {
            return null;
        }

        var secret = AccountDeletionSecrets.NewSecret();
        var session = new AccountDeletionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationId = applicationId,
            SecretHash = AccountDeletionSecrets.Hash(secret),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_settings.SessionMinutes),
        };
        _db.AccountDeletionSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Account deletion session started for user {UserId}", userId);
        return (secret, new DeletionSessionSummary(
            access.User.FirstName, access.User.LastName, access.User.Email, session.ExpiresAt));
    }

    private async Task<AccountDeletionSession?> FindActiveAsync(string secret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var hash = AccountDeletionSecrets.Hash(secret);
        var now = DateTime.UtcNow;
        return await _db.AccountDeletionSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SecretHash == hash && s.UsedAt == null && s.ExpiresAt > now, ct);
    }

    private async Task<DeletionSessionSummary?> SummarizeAsync(AccountDeletionSession session, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == session.UserId, ct);
        return user is null || !user.IsActive
            ? null
            : new DeletionSessionSummary(user.FirstName, user.LastName, user.Email, session.ExpiresAt);
    }

    /// <summary>Aviso al titular. Nunca hace fallar la eliminación.</summary>
    private async Task NotifyAsync(Guid userId, DateTime? purgeAfter, CancellationToken ct)
    {
        try
        {
            var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(user?.Email))
            {
                return;
            }

            var date = purgeAfter?.ToString("yyyy-MM-dd") ?? "-";
            await _emailSender.SendAsync(new EmailMessage(
                user.Email,
                "COPP-ADRESD: solicitud de eliminación de cuenta / account deletion request",
                $"<p>Hola {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>"
                + "<p>Recibimos la solicitud de eliminar tu cuenta de la app COPP-ADRESD. Tu acceso ya está bloqueado "
                + $"y tus datos de identificación se borrarán de forma definitiva a partir del {date}.</p>"
                + "<p>Si no fuiste tú, contáctanos de inmediato.</p><hr>"
                + "<p>We received a request to delete your COPP-ADRESD app account. Your access is already blocked "
                + $"and your identifying data will be permanently erased on or after {date}. "
                + "If this was not you, contact us immediately.</p>"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account deletion notification failed for user {UserId}", userId);
        }
    }
}
