namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Decisión del rate-limiter distribuido (change sos-panic-real, D7).
/// <see cref="RetryAfterSeconds"/> es el valor para el encabezado
/// <c>Retry-After</c> (siempre &gt; 0 cuando <see cref="Allowed"/> es false).
/// </summary>
public sealed record SosRateLimitDecision(bool Allowed, int RetryAfterSeconds, string Reason)
{
    public static SosRateLimitDecision Allow() => new(true, 0, "allowed");

    public static SosRateLimitDecision Deny(int retryAfterSeconds, string reason) =>
        new(false, Math.Max(1, retryAfterSeconds), reason);
}

/// <summary>
/// Rate-limiting distribuido en Valkey para activaciones SOS. Claves aisladas
/// por paciente, teléfono destino y dispositivo (opcional, cuando viaja en
/// cabecera): cooldown 60 s entre intentos, cuota 3/15 min y 10/día, lockout
/// de 15 min ante ráfagas. Se evalúa SIEMPRE antes de persistir la alerta o
/// invocar canales (429 con Retry-After). Prohibido implementarlo en memoria
/// (Singleton): con réplicas el contador no se comparte.
/// Interruptor <c>Sos:RateLimit:Enabled</c> (default false): apagado no
/// aplica cuotas ni registra consumo — el pánico nunca rebota.
/// </summary>
public interface ISosRateLimiter
{
    /// <summary>
    /// Verifica las cuotas del intento SIN consumirlas (el consumo ocurre en
    /// <see cref="RegisterAttemptAsync"/> tras crear la alerta). La clave de
    /// teléfono aplica el límite compartido del número destino; la de
    /// dispositivo solo cuando <paramref name="deviceId"/> viaja en cabecera.
    /// </summary>
    Task<SosRateLimitDecision> CheckAsync(
        Guid patientId,
        string destinationPhoneE164,
        string? deviceId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Registra el intento consumado (cooldown + cuotas por paciente, teléfono
    /// y dispositivo). Se invoca SOLO tras crear la alerta; un 409 de
    /// idempotencia/carrera no consume cuota.
    /// </summary>
    Task RegisterAttemptAsync(
        Guid patientId,
        string destinationPhoneE164,
        string? deviceId,
        CancellationToken ct = default
    );
}
