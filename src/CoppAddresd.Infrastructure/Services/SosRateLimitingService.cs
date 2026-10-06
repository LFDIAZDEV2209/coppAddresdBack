using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Rate-limiter distribuido de SOS sobre Valkey (a través de
/// <see cref="ICacheService"/> — Valkey/Memory/None según entorno, D7). Nada
/// en memoria por réplica: las claves son compartidas entre instancias.
///
/// Cuotas (REQ-SOS-02): cooldown 60 s entre intentos, 3 alertas por ventana
/// de 15 min, 10 por día, lockout 15 min ante ráfagas (3 en ventana →
/// lockout). El check es previo a persistir o invocar canales; el consumo se
/// registra tras crear la alerta (un 409 de carrera/idempotencia no consume).
///
/// Interruptor <c>Sos:RateLimit:Enabled</c> (default false): apagado, el
/// check permite SIEMPRE y no se registra consumo — el botón de pánico nunca
/// rebota por cuota mientras producto no lo pida.
///
/// Contrato fail-open: si Valkey no responde, se PERMITE el intento (Warning
/// en log) — bloquear una emergencia real por una caída de caché es peor que
/// el riesgo de duplicar un SMS (el hard-guarantee de una sola alerta activa
/// vive en el índice parcial de PostgreSQL).
/// </summary>
public sealed class SosRateLimitingService(
    ICacheService cache,
    IOptions<SosRateLimitSettings> settings,
    ILogger<SosRateLimitingService> logger
) : ISosRateLimiter
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Window15m = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WindowDay = TimeSpan.FromHours(24);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private const int MaxPer15m = 3;
    private const int MaxPerDay = 10;
    private const int RetryAfterLockout = 900;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Estado persistido de una ventana de rate-limit: contador + instante de
    /// expiración (Retry-After = expiresAt - now; ICacheService no expone TTL).
    /// Público para que tests y futuros procesadores puedan leer/sembrar el
    /// estado con el mismo shape.
    /// </summary>
    public sealed record SosRateLimitState(int Count, long ExpiresAtEpochSeconds);

    public async Task<SosRateLimitDecision> CheckAsync(
        Guid patientId,
        string destinationPhoneE164,
        string? deviceId,
        CancellationToken ct = default
    )
    {
        // Interruptor de producto: apagado (default), permitir siempre.
        if (!settings.Value.Enabled)
        {
            return SosRateLimitDecision.Allow();
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var nowEpoch = now.ToUnixTimeSeconds();

            // Lockout activo (ráfaga reiterada): bloqueo total del principal
            // (paciente, teléfono o dispositivo — cada uno con su propia
            // ventana de lockout).
            if (await ReadAsync(Key(patientId, "lock"), ct) is { } locked)
            {
                return SosRateLimitDecision.Deny(
                    Remaining(locked.ExpiresAtEpochSeconds, nowEpoch),
                    "lockout"
                );
            }

            if (await ReadAsync(PhoneKey(destinationPhoneE164, "lock"), ct) is { } phoneLock)
            {
                return SosRateLimitDecision.Deny(
                    Remaining(phoneLock.ExpiresAtEpochSeconds, nowEpoch),
                    "lockout-phone"
                );
            }

            if (
                deviceId is not null
                && await ReadAsync(DeviceKey(deviceId, "lock"), ct) is { } deviceLock
            )
            {
                return SosRateLimitDecision.Deny(
                    Remaining(deviceLock.ExpiresAtEpochSeconds, nowEpoch),
                    "lockout-device"
                );
            }

            // Cooldown obligatorio entre intentos (60 s por paciente).
            if (await ReadAsync(Key(patientId, "cooldown"), ct) is { } cooldown)
            {
                return SosRateLimitDecision.Deny(
                    Remaining(cooldown.ExpiresAtEpochSeconds, nowEpoch),
                    "cooldown"
                );
            }

            // Cuota por paciente (ventanas fijas en caché distribuida).
            if (
                await ReadAsync(Key(patientId, "15m"), ct) is { } patient15m
                && patient15m.Count >= MaxPer15m
            )
            {
                return SosRateLimitDecision.Deny(
                    Remaining(patient15m.ExpiresAtEpochSeconds, nowEpoch),
                    "quota-15m"
                );
            }

            if (
                await ReadAsync(Key(patientId, "day"), ct) is { } patientDay
                && patientDay.Count >= MaxPerDay
            )
            {
                return SosRateLimitDecision.Deny(
                    Remaining(patientDay.ExpiresAtEpochSeconds, nowEpoch),
                    "quota-day"
                );
            }

            // Cuota compartida del número destino (anti-bombardeo del contacto).
            var phoneKey = PhoneKey(destinationPhoneE164, "15m");
            if (await ReadAsync(phoneKey, ct) is { } phone15m && phone15m.Count >= MaxPer15m)
            {
                return SosRateLimitDecision.Deny(
                    Remaining(phone15m.ExpiresAtEpochSeconds, nowEpoch),
                    "quota-phone"
                );
            }

            var phoneDayKey = PhoneKey(destinationPhoneE164, "day");
            if (await ReadAsync(phoneDayKey, ct) is { } phoneDay && phoneDay.Count >= MaxPerDay)
            {
                return SosRateLimitDecision.Deny(
                    Remaining(phoneDay.ExpiresAtEpochSeconds, nowEpoch),
                    "quota-phone-day"
                );
            }

            // Cuota por dispositivo (solo cuando viaja en cabecera, D7).
            if (deviceId is not null)
            {
                if (
                    await ReadAsync(DeviceKey(deviceId, "15m"), ct) is { } device15m
                    && device15m.Count >= MaxPer15m
                )
                {
                    return SosRateLimitDecision.Deny(
                        Remaining(device15m.ExpiresAtEpochSeconds, nowEpoch),
                        "quota-device"
                    );
                }

                if (
                    await ReadAsync(DeviceKey(deviceId, "day"), ct) is { } deviceDay
                    && deviceDay.Count >= MaxPerDay
                )
                {
                    return SosRateLimitDecision.Deny(
                        Remaining(deviceDay.ExpiresAtEpochSeconds, nowEpoch),
                        "quota-device-day"
                    );
                }
            }

            return SosRateLimitDecision.Allow();
        }
        catch (Exception ex)
        {
            // Fail-open documentado: Valkey caído no debe bloquear una
            // emergencia; la unicidad estructural queda en PostgreSQL.
            logger.LogWarning(
                ex,
                "Rate-limiter SOS degradado (caché no disponible) — intento permitido sin cuota."
            );
            return SosRateLimitDecision.Allow();
        }
    }

    public async Task RegisterAttemptAsync(
        Guid patientId,
        string destinationPhoneE164,
        string? deviceId,
        CancellationToken ct = default
    )
    {
        // Interruptor de producto: apagado (default), no se consume cuota.
        if (!settings.Value.Enabled)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var nowEpoch = now.ToUnixTimeSeconds();

            // Cooldown: 60 s entre intentos.
            await cache.SetAsync(
                Key(patientId, "cooldown"),
                new SosRateLimitState(1, nowEpoch + (long)Cooldown.TotalSeconds),
                Cooldown,
                ct
            );

            // Ventanas fijas: si la clave expiró, el contador reinicia en 1.
            await IncrementAsync(Key(patientId, "15m"), Window15m, now, nowEpoch, ct);
            await IncrementAsync(Key(patientId, "day"), WindowDay, now, nowEpoch, ct);
            await IncrementAsync(
                PhoneKey(destinationPhoneE164, "15m"),
                Window15m,
                now,
                nowEpoch,
                ct
            );
            await IncrementAsync(
                PhoneKey(destinationPhoneE164, "day"),
                WindowDay,
                now,
                nowEpoch,
                ct
            );

            if (deviceId is not null)
            {
                await IncrementAsync(DeviceKey(deviceId, "15m"), Window15m, now, nowEpoch, ct);
                await IncrementAsync(DeviceKey(deviceId, "day"), WindowDay, now, nowEpoch, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo registrar el intento SOS en caché (fail-open) — patientId={PatientId}.",
                patientId
            );
        }
    }

    /// <summary>
    /// Incremento get-modify-set (no atómico en Valkey vía ICacheService):
    /// bajo ráfaga extrema dos réplicas pueden contar de más — sobrecontar
    /// bloquea, no libera, que es el fallo seguro esperado. El lockout se
    /// dispara al exceder la cuota de 15 min con esta misma cuenta.
    /// </summary>
    private async Task IncrementAsync(
        string key,
        TimeSpan window,
        DateTimeOffset now,
        long nowEpoch,
        CancellationToken ct
    )
    {
        var current = await ReadAsync(key, ct);
        var expiresAt =
            current is not null && current.ExpiresAtEpochSeconds > nowEpoch
                ? current.ExpiresAtEpochSeconds
                : nowEpoch + (long)window.TotalSeconds;
        var count = (current?.Count ?? 0) + 1;

        await cache.SetAsync(key, new SosRateLimitState(count, expiresAt), window, ct);

        // Lockout ante ráfaga: se superó la cuota de ventana al consumir.
        if (key.EndsWith("15m", StringComparison.Ordinal) && count > MaxPer15m)
        {
            await cache.SetAsync(
                key.Replace(":15m", ":lock", StringComparison.Ordinal),
                new SosRateLimitState(count, nowEpoch + (long)Lockout.TotalSeconds),
                Lockout,
                ct
            );
        }
    }

    private async Task<SosRateLimitState?> ReadAsync(string key, CancellationToken ct) =>
        await cache.GetAsync<SosRateLimitState>(key, ct);

    private static int Remaining(long expiresAtEpoch, long nowEpoch) =>
        (int)Math.Max(1, expiresAtEpoch - nowEpoch);

    /// <summary>
    /// Clave namespaced por el prefijo del ICacheService (erp:v1 por defecto)
    /// + <c>sos:ratelimit:</c> (D7). El teléfono NUNCA viaja en logs, solo
    /// en la clave de Valkey (hashable si se requiere; clave opaca).
    /// </summary>
    private static string Key(Guid patientId, string window) =>
        $"sos:ratelimit:patient:{patientId}:{window}";

    private static string PhoneKey(string phoneE164, string window) =>
        $"sos:ratelimit:phone:{phoneE164}:{window}";

    private static string DeviceKey(string deviceId, string window) =>
        $"sos:ratelimit:device:{deviceId}:{window}";
}
