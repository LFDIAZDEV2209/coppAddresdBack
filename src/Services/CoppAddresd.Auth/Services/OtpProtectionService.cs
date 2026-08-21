using System.Collections.Concurrent;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Motor interno de protección del flujo OTP en memoria (thread-safe).
///
/// - SEND: límites por IP (minuto/hora), por teléfono (minuto/hora/día),
///   cooldown por teléfono y límite por documento (hora). La evaluación
///   (<see cref="CheckCanSend"/>) NO consume cuota; el registro
///   (<see cref="RegisterSend"/>) solo se llama cuando el proveedor aceptó.
/// - VERIFY: límite por IP (minuto), ventana por teléfono, máximo de intentos
///   fallidos y lockout temporal.
///
/// Cuando <c>OtpSecuritySettings.Enabled = false</c> todas las operaciones se
/// permiten y no se registra estado alguno.
///
/// Almacenamiento: <see cref="ConcurrentDictionary"/> con estado por clave
/// protegido con <c>lock</c> por entrada. La limpieza de estados expirados se
/// ejecuta de forma oportunista al inicio de cada operación pública.
/// </summary>
public class OtpProtectionService : IOtpProtectionService
{
    private readonly OtpSecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, SendIpState> _sendByIp = new();
    private readonly ConcurrentDictionary<string, SendPhoneState> _sendByPhone = new();
    private readonly ConcurrentDictionary<string, SendDocumentState> _sendByDocument = new();
    private readonly ConcurrentDictionary<string, VerifyIpState> _verifyByIp = new();
    private readonly ConcurrentDictionary<string, VerifyPhoneState> _verifyByPhone = new();

    public OtpProtectionService(
        IOptions<OtpSecuritySettings> settings,
        TimeProvider? timeProvider = null)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OtpProtectionResult CheckCanSend(string ipAddress, string documentNumber, string phoneE164)
    {
        if (!_settings.Enabled)
        {
            return OtpProtectionResult.Allow();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        CleanupExpired(now);

        var ip = _sendByIp.GetOrAdd(NormalizeKey(ipAddress), static _ => new SendIpState());
        lock (ip)
        {
            if (!WindowsWithinLimit(
                    ip.Minute, TimeSpan.FromMinutes(1), _settings.SendPerIpPerMinute,
                    ip.Hour, TimeSpan.FromHours(1), _settings.SendPerIpPerHour,
                    now, out var retry))
            {
                return OtpProtectionResult.Block(OtpProtectionReason.SendIpRateLimit, retry);
            }
        }

        var phone = _sendByPhone.GetOrAdd(NormalizeKey(phoneE164), static _ => new SendPhoneState());
        lock (phone)
        {
            if (!WindowsWithinLimit(
                    phone.Minute, TimeSpan.FromMinutes(1), _settings.SendPerPhonePerMinute,
                    phone.Hour, TimeSpan.FromHours(1), _settings.SendPerPhonePerHour,
                    phone.Day, TimeSpan.FromDays(1), _settings.SendPerPhonePerDay,
                    now, out var retry))
            {
                return OtpProtectionResult.Block(OtpProtectionReason.SendPhoneRateLimit, retry);
            }

            if (_settings.SendPhoneCooldownSeconds > 0 && phone.LastSendUtc != default)
            {
                var remaining = _settings.SendPhoneCooldownSeconds
                                - (now - phone.LastSendUtc).TotalSeconds;
                if (remaining > 0)
                {
                    return OtpProtectionResult.Block(
                        OtpProtectionReason.SendPhoneCooldown,
                        Math.Max(1, (int)Math.Ceiling(remaining)));
                }
            }
        }

        var document = _sendByDocument.GetOrAdd(NormalizeKey(documentNumber), static _ => new SendDocumentState());
        lock (document)
        {
            if (!WindowWithinLimit(
                    document.Hour, TimeSpan.FromHours(1), _settings.SendPerDocumentPerHour,
                    now, out var retry))
            {
                return OtpProtectionResult.Block(OtpProtectionReason.SendDocumentRateLimit, retry);
            }
        }

        return OtpProtectionResult.Allow();
    }

    public void RegisterSend(string ipAddress, string documentNumber, string phoneE164)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        CleanupExpired(now);

        var ip = _sendByIp.GetOrAdd(NormalizeKey(ipAddress), static _ => new SendIpState());
        lock (ip)
        {
            ip.Minute.Count(now, TimeSpan.FromMinutes(1));
            ip.Hour.Count(now, TimeSpan.FromHours(1));
        }

        var phone = _sendByPhone.GetOrAdd(NormalizeKey(phoneE164), static _ => new SendPhoneState());
        lock (phone)
        {
            phone.Minute.Count(now, TimeSpan.FromMinutes(1));
            phone.Hour.Count(now, TimeSpan.FromHours(1));
            phone.Day.Count(now, TimeSpan.FromDays(1));
            phone.LastSendUtc = now;
        }

        var document = _sendByDocument.GetOrAdd(NormalizeKey(documentNumber), static _ => new SendDocumentState());
        lock (document)
        {
            document.Hour.Count(now, TimeSpan.FromHours(1));
        }
    }

    public OtpProtectionResult CheckCanVerify(string ipAddress, string phoneE164)
    {
        if (!_settings.Enabled)
        {
            return OtpProtectionResult.Allow();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        CleanupExpired(now);

        var phone = _verifyByPhone.GetOrAdd(NormalizeKey(phoneE164), static _ => new VerifyPhoneState());
        lock (phone)
        {
            if (phone.LockoutUntilUtc is { } lockoutUntil && lockoutUntil > now)
            {
                var retry = Math.Max(1, (int)Math.Ceiling((lockoutUntil - now).TotalSeconds));
                return OtpProtectionResult.Block(OtpProtectionReason.VerifyPhoneLocked, retry);
            }

            if (!WindowWithinLimit(
                    phone.Window,
                    TimeSpan.FromMinutes(_settings.VerifyPerPhoneWindowMinutes),
                    _settings.VerifyPerPhoneLimit,
                    now, out var retryPhone))
            {
                return OtpProtectionResult.Block(OtpProtectionReason.VerifyPhoneRateLimit, retryPhone);
            }
        }

        var ip = _verifyByIp.GetOrAdd(NormalizeKey(ipAddress), static _ => new VerifyIpState());
        lock (ip)
        {
            if (!WindowWithinLimit(
                    ip.Minute,
                    TimeSpan.FromMinutes(1),
                    _settings.VerifyPerIpPerMinute,
                    now, out var retryIp))
            {
                return OtpProtectionResult.Block(OtpProtectionReason.VerifyIpRateLimit, retryIp);
            }
        }

        return OtpProtectionResult.Allow();
    }

    public void RegisterVerifyFailed(string ipAddress, string phoneE164)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        CleanupExpired(now);

        var ip = _verifyByIp.GetOrAdd(NormalizeKey(ipAddress), static _ => new VerifyIpState());
        lock (ip)
        {
            ip.Minute.Count(now, TimeSpan.FromMinutes(1));
        }

        var phone = _verifyByPhone.GetOrAdd(NormalizeKey(phoneE164), static _ => new VerifyPhoneState());
        lock (phone)
        {
            phone.Window.Count(now, TimeSpan.FromMinutes(_settings.VerifyPerPhoneWindowMinutes));

            if (_settings.VerifyPhoneMaxFailedAttempts > 0)
            {
                phone.FailedAttempts++;
                if (phone.FailedAttempts >= _settings.VerifyPhoneMaxFailedAttempts)
                {
                    // Activa el lockout y reinicia el contador para que, tras
                    // expirar el lockout, el teléfono disponga de un nuevo
                    // juego de intentos.
                    if (_settings.VerifyPhoneLockoutSeconds > 0)
                    {
                        phone.LockoutUntilUtc = now.AddSeconds(_settings.VerifyPhoneLockoutSeconds);
                    }

                    phone.FailedAttempts = 0;
                }
            }
        }
    }

    public void RegisterVerifySucceeded(string ipAddress, string phoneE164)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        CleanupExpired(now);

        var ip = _verifyByIp.GetOrAdd(NormalizeKey(ipAddress), static _ => new VerifyIpState());
        lock (ip)
        {
            ip.Minute.Count(now, TimeSpan.FromMinutes(1));
        }

        var phone = _verifyByPhone.GetOrAdd(NormalizeKey(phoneE164), static _ => new VerifyPhoneState());
        lock (phone)
        {
            phone.Window.Count(now, TimeSpan.FromMinutes(_settings.VerifyPerPhoneWindowMinutes));
            phone.FailedAttempts = 0;
            phone.LockoutUntilUtc = null;
        }
    }

    // ---------------------------------------------------------------------
    // Ventanas (lectura sin mutación y registro de conteo)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Evalúa varias ventanas a la vez sin mutar estado. Devuelve true si todas
    /// están por debajo de su límite y, si alguna la excede, <paramref name="retryAfterSeconds"/>
    /// con el tiempo restante de la ventana que más tarde se libera.
    /// </summary>
    private static bool WindowsWithinLimit(
        WindowState w1, TimeSpan len1, int limit1,
        WindowState w2, TimeSpan len2, int limit2,
        DateTime now, out int retryAfterSeconds)
    {
        var ok = WindowWithinLimit(w1, len1, limit1, now, out var r1);
        var ok2 = WindowWithinLimit(w2, len2, limit2, now, out var r2);
        retryAfterSeconds = Math.Max(r1, r2);
        return ok && ok2;
    }

    private static bool WindowsWithinLimit(
        WindowState w1, TimeSpan len1, int limit1,
        WindowState w2, TimeSpan len2, int limit2,
        WindowState w3, TimeSpan len3, int limit3,
        DateTime now, out int retryAfterSeconds)
    {
        var ok1 = WindowWithinLimit(w1, len1, limit1, now, out var r1);
        var ok2 = WindowWithinLimit(w2, len2, limit2, now, out var r2);
        var ok3 = WindowWithinLimit(w3, len3, limit3, now, out var r3);
        retryAfterSeconds = Math.Max(r1, Math.Max(r2, r3));
        return ok1 && ok2 && ok3;
    }

    /// <summary>
    /// Evalúa una ventana sin mutar estado: considera la ventana expirada como
    /// vacía. Si está bloqueada, calcula los segundos restantes hasta que la
    /// ventana se reinicie.
    /// </summary>
    private static bool WindowWithinLimit(
        WindowState window, TimeSpan length, int limit, DateTime now, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (!window.WouldBeWithinLimit(now, length, limit, out var effectiveCount, out var resetAt))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((resetAt - now).TotalSeconds));
            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Limpieza oportunista de estados expirados
    // ---------------------------------------------------------------------

    private void CleanupExpired(DateTime now)
    {
        foreach (var entry in _sendByIp)
        {
            lock (entry.Value)
            {
                if (!entry.Value.IsActive(now))
                {
                    _sendByIp.TryRemove(entry.Key, out _);
                }
            }
        }

        foreach (var entry in _sendByPhone)
        {
            lock (entry.Value)
            {
                if (!entry.Value.IsActive(now, TimeSpan.FromSeconds(_settings.SendPhoneCooldownSeconds)))
                {
                    _sendByPhone.TryRemove(entry.Key, out _);
                }
            }
        }

        foreach (var entry in _sendByDocument)
        {
            lock (entry.Value)
            {
                if (!entry.Value.IsActive(now))
                {
                    _sendByDocument.TryRemove(entry.Key, out _);
                }
            }
        }

        foreach (var entry in _verifyByIp)
        {
            lock (entry.Value)
            {
                if (!entry.Value.IsActive(now))
                {
                    _verifyByIp.TryRemove(entry.Key, out _);
                }
            }
        }

        foreach (var entry in _verifyByPhone)
        {
            lock (entry.Value)
            {
                if (!entry.Value.IsActive(now, TimeSpan.FromMinutes(_settings.VerifyPerPhoneWindowMinutes)))
                {
                    _verifyByPhone.TryRemove(entry.Key, out _);
                }
            }
        }
    }

    private static string NormalizeKey(string value)
        => (value ?? string.Empty).Trim();

    // ---------------------------------------------------------------------
    // Estados internos (por clave, con ventanas)
    // ---------------------------------------------------------------------

    /// <summary>Ventana de conteo con reinicio automático al expirar.</summary>
    private sealed class WindowState
    {
        private DateTime _startUtc;
        private int _count;

        /// <summary>
        /// Evaluación sin mutación. Devuelve true si la ventana está por debajo
        /// del límite; en caso contrario expone el conteo efectivo y el instante
        /// en que la ventana se reinicia.
        /// </summary>
        public bool WouldBeWithinLimit(DateTime now, TimeSpan length, int limit, out int effectiveCount, out DateTime resetAt)
        {
            effectiveCount = _count;
            resetAt = _startUtc + length;
            if (effectiveCount > 0 && now >= resetAt)
            {
                effectiveCount = 0;
            }

            return effectiveCount < limit;
        }

        /// <summary>Registra un evento: reinicia la ventana si ya expiró y suma uno.</summary>
        public void Count(DateTime now, TimeSpan length)
        {
            if (_count > 0 && now >= _startUtc + length)
            {
                _startUtc = now;
                _count = 1;
                return;
            }

            if (_count == 0)
            {
                _startUtc = now;
            }

            _count++;
        }

        /// <summary>La ventana está activa si tiene conteo y no ha expirado.</summary>
        public bool IsActive(DateTime now, TimeSpan length)
            => _count > 0 && now < _startUtc + length;
    }

    private sealed class SendIpState
    {
        public WindowState Minute { get; } = new();
        public WindowState Hour { get; } = new();

        public bool IsActive(DateTime now)
            => Minute.IsActive(now, TimeSpan.FromMinutes(1))
               || Hour.IsActive(now, TimeSpan.FromHours(1));
    }

    private sealed class SendPhoneState
    {
        public WindowState Minute { get; } = new();
        public WindowState Hour { get; } = new();
        public WindowState Day { get; } = new();
        public DateTime LastSendUtc { get; set; }

        public bool IsActive(DateTime now, TimeSpan cooldown)
        {
            if (Minute.IsActive(now, TimeSpan.FromMinutes(1))
                || Hour.IsActive(now, TimeSpan.FromHours(1))
                || Day.IsActive(now, TimeSpan.FromDays(1)))
            {
                return true;
            }

            return LastSendUtc != default && now - LastSendUtc < cooldown;
        }
    }

    private sealed class SendDocumentState
    {
        public WindowState Hour { get; } = new();

        public bool IsActive(DateTime now)
            => Hour.IsActive(now, TimeSpan.FromHours(1));
    }

    private sealed class VerifyIpState
    {
        public WindowState Minute { get; } = new();

        public bool IsActive(DateTime now)
            => Minute.IsActive(now, TimeSpan.FromMinutes(1));
    }

    private sealed class VerifyPhoneState
    {
        public WindowState Window { get; } = new();
        public int FailedAttempts { get; set; }
        public DateTime? LockoutUntilUtc { get; set; }

        public bool IsActive(DateTime now, TimeSpan window)
        {
            if (Window.IsActive(now, window))
            {
                return true;
            }

            if (FailedAttempts > 0)
            {
                return true;
            }

            return LockoutUntilUtc is { } lu && lu > now;
        }
    }
}
