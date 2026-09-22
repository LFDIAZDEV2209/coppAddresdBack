using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Exceptions;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Primer inicio de sesión por número de identificación.
///
/// Flujo:
///  1. <see cref="LookupByIdAsync"/>: el usuario ingresa su ID; se devuelven
///     los correos/teléfonos asociados (enmascarados) para que elija el canal.
///  2. <see cref="SendOtpAsync"/>: se envía un OTP al destino elegido.
///     Canal PHONE → Twilio Verify V2 (SMS): antes de consumir Twilio pasa por
///     el motor de protección (<see cref="IOtpProtectionService"/>) que limita
///     envíos por IP/teléfono/documento y cooldown; el envío solo se registra
///     si Twilio aceptó. Canal EMAIL → flujo local: código de 6 dígitos
///     persistido solo como hash (SHA-256 + salt) en auth.otp_codes (sin motor
///     de protección en esta fase).
///  3. <see cref="VerifyOtpAsync"/>: el canal PHONE también pasa por el motor
///     (límites por IP/teléfono + intentos fallidos con lockout) y registra el
///     resultado (fallo/éxito). Si el código es válido (Twilio approved o hash
///     EMAIL correcto) se aprovisiona la cuenta (se crea el usuario si no
///     existe, se vincula al perfil del paciente y se le otorga acceso a la
///     aplicación) y se emiten los tokens.
/// </summary>
public class OtpService : IOtpService
{
    private const int OtpLifetimeSeconds = 300;
    private const int MaxAttempts = 5;
    private const string ContactEmail = "email";
    private const string ContactPhone = "phone";

    // Valor persistido en auth.otp_codes.Channel (los ids de contacto del
    // request son minúsculas: "email"/"phone"; el canal resuelto es con
    // mayúscula inicial).
    private const string ChannelEmail = "Email";
    private const string ChannelPhone = "Phone";

    private readonly IPatientLookupService _patientLookup;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPermissionService _permissionService;
    private readonly ITwilioOtpService _twilioOtpService;
    private readonly IOtpProtectionService _otpProtection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JwtSettings _jwtSettings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IPatientLookupService patientLookup,
        UserManager<ApplicationUser> userManager,
        AuthDbContext dbContext,
        ITokenService tokenService,
        IPermissionService permissionService,
        ITwilioOtpService twilioOtpService,
        IOtpProtectionService otpProtection,
        IHttpContextAccessor httpContextAccessor,
        IOptions<JwtSettings> jwtSettings,
        IHostEnvironment environment,
        ILogger<OtpService> logger
    )
    {
        _patientLookup = patientLookup;
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _permissionService = permissionService;
        _twilioOtpService = twilioOtpService;
        _otpProtection = otpProtection;
        _httpContextAccessor = httpContextAccessor;
        _jwtSettings = jwtSettings.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IdLookupResponse?> LookupByIdAsync(
        IdLookupRequest request,
        CancellationToken ct = default
    )
    {
        var patient = await _patientLookup.FindByDocumentNumberAsync(request.DocumentNumber, ct);
        if (patient is null)
        {
            _logger.LogInformation("Id lookup: no patient for document");
            return null;
        }

        return new IdLookupResponse(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DocumentNumber,
            BuildContacts(patient)
        );
    }

    public async Task<(bool Success, string? Error, SendOtpResponse? Result)> SendOtpAsync(
        SendOtpRequest request,
        CancellationToken ct = default
    )
    {
        var patient = await _patientLookup.FindByDocumentNumberAsync(request.DocumentNumber, ct);
        if (patient is null)
        {
            _logger.LogWarning("OTP send: no patient for document");
            return (false, "El número de identificación no está registrado", null);
        }

        var (channel, target) = ResolveContact(patient, request.ContactId);
        if (channel.Length == 0 || target is null)
        {
            _logger.LogWarning(
                "OTP send: contact {ContactId} not available for document {Document}",
                request.ContactId,
                patient.DocumentNumber
            );
            return (false, "Ese método de contacto no está disponible", null);
        }

        // Canal PHONE → Twilio Verify V2 (SMS). Twilio genera el código, envía
        // el SMS y conserva temporalmente el estado de verificación: no se
        // genera, hashea ni persiste OTP local, y en Development tampoco se
        // devuelve código alguno (Twilio es la autoridad también en desarrollo).
        if (IsPhoneChannel(channel))
        {
            var clientIp = GetClientIp();

            // Guard del motor de protección (IP + documento + teléfono E.164):
            // no consume cuota; si bloquea, no se toca Twilio ni se persiste nada.
            var guard = _otpProtection.CheckCanSend(clientIp, patient.DocumentNumber, target);
            if (!guard.Allowed)
            {
                _logger.LogWarning(
                    "OTP send blocked (Reason={Reason}) for document {Document} from ip {Ip}",
                    guard.Reason,
                    MaskDocument(patient.DocumentNumber),
                    clientIp
                );
                throw new OtpProtectionException(
                    guard.Reason!.Value,
                    "Demasiadas peticiones. Intenta más tarde.",
                    guard.RetryAfterSeconds
                );
            }

            await _twilioOtpService.SendAsync(target, ct);

            // Solo si Twilio aceptó la verificación se registra el envío (la
            // cuota local no se consume cuando el proveedor falla).
            _otpProtection.RegisterSend(clientIp, patient.DocumentNumber, target);

            // Solo si Twilio confirmó el envío se invalidan códigos EMAIL
            // pendientes del documento: únicamente el último OTP emitido (de
            // cualquier canal) es válido, evitando que un código de correo
            // previo secuestre la verificación por teléfono.
            await _dbContext
                .OtpCodes.Where(o => o.DocumentNumber == patient.DocumentNumber && o.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.UsedAt, DateTime.UtcNow), ct);

            _logger.LogInformation(
                "OTP phone verification requested for document {Document}",
                patient.DocumentNumber
            );

            return (true, null, new SendOtpResponse(OtpLifetimeSeconds));
        }

        // Canal EMAIL → flujo local existente: hash SHA-256 + salt +
        // auth.otp_codes (sin cambios en esta fase).
        // Invalida códigos pendientes previos del mismo documento+canal: solo
        // el último OTP emitido es válido (evita códigos huérfanos reusables).
        await _dbContext
            .OtpCodes.Where(o =>
                o.DocumentNumber == patient.DocumentNumber
                && o.Channel == channel
                && o.UsedAt == null
            )
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.UsedAt, DateTime.UtcNow), ct);

        var code = GenerateOtp();
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        _dbContext.OtpCodes.Add(
            new OtpCode
            {
                Id = Guid.NewGuid(),
                DocumentNumber = patient.DocumentNumber,
                Channel = channel,
                Target = target,
                CodeHash = HashCode(salt, code),
                Salt = salt,
                ExpiresAt = DateTime.UtcNow.AddSeconds(OtpLifetimeSeconds),
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _dbContext.SaveChangesAsync(ct);

        // "Envío" del código. En desarrollo se devuelve en la respuesta para
        // habilitar pruebas end-to-end sin proveedor real; en producción se
        // integraría un proveedor de correo/SMS. Nunca se loguea el código.
        var devCode = _environment.IsDevelopment() ? code : null;

        _logger.LogInformation(
            "OTP issued for document {Document} via {Channel} (expires in {Seconds}s)",
            patient.DocumentNumber,
            channel,
            OtpLifetimeSeconds
        );

        return (true, null, new SendOtpResponse(OtpLifetimeSeconds, devCode));
    }

    public async Task<TokenResult?> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken ct = default
    )
    {
        var patient = await _patientLookup.FindByDocumentNumberAsync(request.DocumentNumber, ct);
        if (patient is null)
        {
            _logger.LogWarning("OTP verify: no patient for document");
            return null;
        }

        // Canal PHONE → Twilio Verify: no persiste nada en auth.otp_codes, por
        // lo que la existencia de un código EMAIL pendiente es el discriminador
        // de canal (toda fila pendiente de este documento solo puede ser EMAIL).
        var otp = await _dbContext
            .OtpCodes.Where(o =>
                o.DocumentNumber == patient.DocumentNumber
                && o.Channel == ChannelEmail
                && o.UsedAt == null
            )
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is not null)
        {
            // Flujo EMAIL local sin cambios: expiración, intentos y hash local.
            if (otp.IsExpired)
            {
                _logger.LogWarning(
                    "OTP verify: code expired for document {Document}",
                    patient.DocumentNumber
                );
                return null;
            }

            if (otp.Attempts >= MaxAttempts)
            {
                _logger.LogWarning(
                    "OTP verify: too many attempts for document {Document}",
                    patient.DocumentNumber
                );
                return null;
            }

            // Comparación en tiempo constante: evita timing attacks sobre el hash.
            if (!FixedTimeEquals(HashCode(otp.Salt, request.Otp.Trim()), otp.CodeHash))
            {
                otp.Attempts += 1;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "OTP verify: invalid code for document {Document} (attempt {Attempts})",
                    patient.DocumentNumber,
                    otp.Attempts
                );
                return null;
            }

            otp.UsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            // Canal PHONE → Twilio Verify. Un código incorrecto devuelve
            // IsApproved=false (sin excepción); errores del proveedor (429,
            // red, etc.) se propagan como TwilioOtpException hacia el
            // GlobalExceptionHandlerMiddleware.
            var phone = ResolveContact(patient, ContactPhone);
            if (phone.Channel.Length == 0 || phone.Target is null)
            {
                _logger.LogWarning(
                    "OTP verify: phone not available for document {Document}",
                    patient.DocumentNumber
                );
                return null;
            }

            var clientIp = GetClientIp();

            // Guard del motor de protección (IP + teléfono E.164): bloqueado
            // (lockout o límites) → 429 sin llamar a Twilio ni aprovisionar.
            var guard = _otpProtection.CheckCanVerify(clientIp, phone.Target);
            if (!guard.Allowed)
            {
                _logger.LogWarning(
                    "OTP verify blocked (Reason={Reason}) for phone {Phone} from ip {Ip}",
                    guard.Reason,
                    MaskE164Phone(phone.Target),
                    clientIp
                );
                throw new OtpProtectionException(
                    guard.Reason!.Value,
                    "Demasiadas peticiones. Intenta más tarde.",
                    guard.RetryAfterSeconds
                );
            }

            var check = await _twilioOtpService.CheckAsync(phone.Target, request.Otp, ct);

            if (!check.IsApproved)
            {
                // El intento que alcanza el máximo de fallos activa el lockout
                // aquí; la siguiente comprobación (CheckCanVerify) lo detecta.
                _otpProtection.RegisterVerifyFailed(clientIp, phone.Target);
                _logger.LogWarning(
                    "OTP phone verification rejected for document {Document}",
                    patient.DocumentNumber
                );
                return null;
            }

            _otpProtection.RegisterVerifySucceeded(clientIp, phone.Target);
            _logger.LogInformation(
                "OTP phone verification approved for document {Document}",
                patient.DocumentNumber
            );
        }

        // Aprovisionamiento: usuario + vínculo con el perfil + acceso a la app.
        var user = await FindOrCreateUserAsync(patient, ct);
        var application = await EnsureApplicationAccessAsync(user.Id, request.Application, ct);
        if (application is null)
        {
            _logger.LogWarning(
                "OTP verify: application {Application} not available for user {UserId}",
                request.Application,
                user.Id
            );
            return null;
        }

        _logger.LogInformation(
            "User {UserId} verified OTP and logged in to application {Application}",
            user.Id,
            application.Code
        );

        return await IssueTokensAsync(user, application, ct);
    }

    private static IReadOnlyList<ContactMethodResponse> BuildContacts(PatientLookupResult patient)
    {
        var contacts = new List<ContactMethodResponse>(2);

        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            contacts.Add(
                new ContactMethodResponse(ContactEmail, "Email", MaskEmail(patient.Email))
            );
        }

        if (!string.IsNullOrWhiteSpace(patient.PhoneNumber))
        {
            contacts.Add(
                new ContactMethodResponse(
                    ContactPhone,
                    "Phone",
                    MaskPhone(patient.PhoneCountryCode, patient.PhoneNumber)
                )
            );
        }

        return contacts;
    }

    private static (string Channel, string? Target) ResolveContact(
        PatientLookupResult patient,
        string contactId
    )
    {
        return contactId.Trim().ToLowerInvariant() switch
        {
            ContactEmail when !string.IsNullOrWhiteSpace(patient.Email) => (
                ChannelEmail,
                patient.Email
            ),

            ContactPhone when !string.IsNullOrWhiteSpace(patient.PhoneNumber) => (
                ChannelPhone,
                FormatPhone(patient.PhoneCountryCode, patient.PhoneNumber)
            ),

            _ => (string.Empty, null),
        };
    }

    private static bool IsPhoneChannel(string channel) =>
        string.Equals(channel, ChannelPhone, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Dirección IP del cliente desde <see cref="IHttpContextAccessor"/>: misma
    /// fuente que el rate limiter global (<c>RemoteIpAddress</c>). No se confía
    /// en headers (<c>X-Forwarded-For</c>): el proyecto no configura
    /// ForwardedHeaders. Si no hay conexión/contexto se usa "unknown".
    /// </summary>
    private string GetClientIp()
    {
        var address = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        return address?.ToString() ?? "unknown";
    }

    /// <summary>Enmascara un documento para logs: conserva los 2 primeros y 2 últimos dígitos.</summary>
    private static string MaskDocument(string documentNumber)
    {
        if (string.IsNullOrEmpty(documentNumber))
        {
            return "****";
        }

        if (documentNumber.Length <= 4)
        {
            return new string('*', documentNumber.Length);
        }

        return documentNumber[..2]
            + new string('*', documentNumber.Length - 4)
            + documentNumber[^2..];
    }

    /// <summary>Enmascara un teléfono E.164 para logs: conserva el '+' y los últimos 4 dígitos.</summary>
    private static string MaskE164Phone(string phoneE164)
    {
        if (string.IsNullOrEmpty(phoneE164))
        {
            return "****";
        }

        if (phoneE164.Length <= 5)
        {
            return new string('*', phoneE164.Length);
        }

        return "+" + new string('*', phoneE164.Length - 5) + phoneE164[^4..];
    }

    /// <summary>
    /// Convierte el teléfono del paciente a E.164 (sin espacios): concatena el
    /// código de país con los dígitos del número. Ej.: (1, "5765550100") →
    /// "+15765550100"; (57, "3001234567") → "+573001234567". No agrega "+1"
    /// por defecto ni adivina el país: usa los campos existentes
    /// <see cref="PatientLookupResult.PhoneCountryCode"/> y
    /// <see cref="PatientLookupResult.PhoneNumber"/>.
    /// </summary>
    private static string FormatPhone(string? countryCode, string phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var prefix = string.IsNullOrWhiteSpace(countryCode) ? string.Empty : $"+{countryCode}";
        return $"{prefix}{digits}";
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***" + email;
        }

        var local = email[..at];
        var domain = email[at..];
        var keep = Math.Min(2, local.Length);
        var masked = local[..keep] + new string('•', Math.Max(3, local.Length - keep));
        return masked + domain;
    }

    private static string MaskPhone(string? countryCode, string phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var prefix = string.IsNullOrWhiteSpace(countryCode) ? string.Empty : $"+{countryCode} ";
        if (digits.Length < 4)
        {
            return $"{prefix}••••";
        }

        var start = digits[..2];
        var end = digits[^2..];
        return $"{prefix}{start}•••{end}";
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string HashCode(string salt, string code)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + code.Trim());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b)
        );
    }

    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "@#$%&*!?";
        const string all = upper + lower + digits + special;

        var password = new char[16];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var i = 4; i < password.Length; i++)
        {
            password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        return new string(password);
    }

    private async Task<ApplicationUser> FindOrCreateUserAsync(
        PatientLookupResult patient,
        CancellationToken ct
    )
    {
        // Ya vinculado a un usuario de Identity: usarlo directamente.
        if (patient.UserId is Guid existingUserId)
        {
            var existing = await _userManager.FindByIdAsync(existingUserId.ToString());
            if (existing is not null)
            {
                return existing;
            }
        }

        // ¿Existe ya un usuario con el correo del paciente (creado desde ERP)?
        // Si es así se vincula el perfil del paciente a ese usuario.
        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            var byEmail = await _userManager.FindByEmailAsync(patient.Email);
            if (byEmail is not null)
            {
                await LinkPatientAsync(patient.Id, byEmail.Id, ct);
                return byEmail;
            }
        }

        // Provisiona un nuevo usuario. El login futuro usa el flujo OTP por
        // identificación: el password se genera aleatorio (inutilizable) y el
        // usuario solo puede entrar verificando su identidad por OTP.
        var userName = !string.IsNullOrWhiteSpace(patient.Email)
            ? patient.Email
            : $"p-{patient.DocumentNumber}";

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = string.IsNullOrWhiteSpace(patient.Email) ? null : patient.Email,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(
                "OTP verify: provisioning failed for patient {PatientId}: {Errors}",
                patient.Id,
                errors
            );
            throw new InvalidOperationException("No se pudo crear la cuenta del paciente");
        }

        await LinkPatientAsync(patient.Id, user.Id, ct);
        return user;
    }

    private async Task<Application?> EnsureApplicationAccessAsync(
        Guid userId,
        string applicationCode,
        CancellationToken ct
    )
    {
        var application = await _dbContext
            .Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == applicationCode, ct);

        if (application is null || !application.IsActive)
        {
            return null;
        }

        var access = await _dbContext
            .UserApplications.AsNoTracking()
            .SingleOrDefaultAsync(
                ua => ua.UserId == userId && ua.ApplicationId == application.Id,
                ct
            );

        if (access?.IsSuspended == true)
            return null;

        if (access is null)
        {
            _dbContext.UserApplications.Add(
                new UserApplication
                {
                    UserId = userId,
                    ApplicationId = application.Id,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Application {Application} access granted to user {UserId}",
                application.Code,
                userId
            );
        }

        return application;
    }

    private async Task LinkPatientAsync(Guid patientId, Guid userId, CancellationToken ct)
    {
        const string sql = """
            UPDATE app.patient_profiles
            SET user_id = {0}
            WHERE id = {1}
              AND user_id IS NULL
            """;
        // Sobrecarga con IEnumerable<object> + ct: la versión params object[]
        // boxearía el CancellationToken como parámetro de la query.
        await _dbContext.Database.ExecuteSqlRawAsync(sql, new object[] { userId, patientId }, ct);
    }

    private async Task<TokenResult?> IssueTokensAsync(
        ApplicationUser user,
        Application application,
        CancellationToken ct
    )
    {
        var access = await _dbContext
            .UserApplications.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == user.Id && x.ApplicationId == application.Id,
                ct
            );
        if (access is null || access.IsSuspended)
            return null;
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetUserAllPermissionCodesAsync(user.Id, ct);
        var accessToken = _tokenService.GenerateAccessToken(
            user,
            roles,
            application.Code,
            permissions,
            access.SessionVersion
        );
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(
            user.Id,
            application.Id,
            ct,
            access.SessionVersion
        );

        return new TokenResult(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: "Bearer",
            ExpiresIn: _jwtSettings.AccessTokenExpirationMinutes * 60
        );
    }
}
