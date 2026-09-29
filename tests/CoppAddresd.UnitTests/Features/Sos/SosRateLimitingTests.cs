using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Cache;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas del rate-limiter distribuido SOS (task 2.1, REQ-SOS-02) con
/// simulación de Valkey (MemoryCacheService real). Cubre cooldown 60s, cuota
/// 3/15min, 10/día, lockout ante ráfagas, cuota por teléfono compartida y
/// fail-open ante caché caída.
/// </summary>
public sealed class SosRateLimitingTests
{
    private const string Phone = "+573053924819";

    private static ICacheService NewCache() =>
        new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            "erp",
            NullLogger<MemoryCacheService>.Instance
        );

    private static SosRateLimitingService NewLimiter(ICacheService cache) =>
        new(cache, NullLogger<SosRateLimitingService>.Instance);

    [Fact]
    public async Task PrimerIntento_Permitido()
    {
        var limiter = NewLimiter(NewCache());

        var decision = await limiter.CheckAsync(
            Guid.NewGuid(),
            Phone,
            null,
            CancellationToken.None
        );

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task Cooldown_SegundoIntentoEnMenosDe60s_DenegadoConRetryAfter()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);
        var patientId = Guid.NewGuid();

        await limiter.RegisterAttemptAsync(patientId, Phone, null, CancellationToken.None);
        var decision = await limiter.CheckAsync(patientId, Phone, null, CancellationToken.None);

        // (REQ-SOS-02) 429 con Retry-After > 0 ANTES de tocar Twilio/FCM.
        Assert.False(decision.Allowed);
        Assert.Equal("cooldown", decision.Reason);
        Assert.InRange(decision.RetryAfterSeconds, 1, 60);
    }

    [Fact]
    public async Task Cooldown_DiferenciaEntrePacientes_NoSeContagia()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);

        await limiter.RegisterAttemptAsync(Guid.NewGuid(), Phone, null, CancellationToken.None);

        // Otro paciente sin intentos propios: el cooldown es por paciente.
        var other = await limiter.CheckAsync(Guid.NewGuid(), Phone, null, CancellationToken.None);
        Assert.True(other.Allowed);
    }

    [Fact]
    public async Task CuotaVentana15m_CuartaAlerta_Denegada()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);
        var patientId = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            await limiter.RegisterAttemptAsync(patientId, Phone, null, CancellationToken.None);
            // El cooldown bloquearía el 2º/3º intento real: se consume para
            // seguir probando la cuota de ventana (semántica de test).
            // Sin prefijo: ICacheService antepone el namespace del servicio.
            await cache.RemoveAsync($"sos:ratelimit:patient:{patientId}:cooldown");
        }

        var decision = await limiter.CheckAsync(patientId, Phone, null, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("quota-15m", decision.Reason);
    }

    [Fact]
    public async Task CuotaVentana15m_ExcesoActivaLockoutDe15Minutos()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);
        var patientId = Guid.NewGuid();

        for (var i = 0; i < 4; i++)
        {
            await limiter.RegisterAttemptAsync(patientId, Phone, null, CancellationToken.None);
            // El cooldown real se consume para poder registrar los 4 intentos.
            await cache.RemoveAsync($"sos:ratelimit:patient:{patientId}:cooldown");
        }

        var decision = await limiter.CheckAsync(patientId, Phone, null, CancellationToken.None);

        // (D7) Ráfaga reiterada → lockout con Retry-After en el orden de
        // minutos (no segundos).
        Assert.False(decision.Allowed);
        Assert.Equal("lockout", decision.Reason);
        Assert.InRange(decision.RetryAfterSeconds, 600, 900);
    }

    [Fact]
    public async Task CuotaDiaria_DecimaAlerta_Denegada()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);
        var patientId = Guid.NewGuid();

        // Se siembra la ventana diaria completa con el MISMO tipo de estado
        // del servicio (semántica: 10 activaciones ya registradas hoy).
        await cache.SetAsync(
            $"sos:ratelimit:patient:{patientId}:day",
            new SosRateLimitingService.SosRateLimitState(
                10,
                DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
            ),
            TimeSpan.FromHours(24),
            CancellationToken.None
        );

        var decision = await limiter.CheckAsync(patientId, Phone, null, CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal("quota-day", decision.Reason);
    }

    [Fact]
    public async Task CuotaCompartidaPorTelefono_TercerDestinatarioDistinto_BloqueaAlCuarto()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);

        // Tres pacientes distintos con el MISMO teléfono destino (contacto
        // compartido): la cuota del teléfono se agota.
        for (var i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid();
            await limiter.RegisterAttemptAsync(patientId, Phone, null, CancellationToken.None);
            await cache.RemoveAsync($"sos:ratelimit:patient:{patientId}:cooldown");
        }

        var newcomer = await limiter.CheckAsync(
            Guid.NewGuid(),
            Phone,
            null,
            CancellationToken.None
        );

        Assert.False(newcomer.Allowed);
        Assert.Equal("quota-phone", newcomer.Reason);
    }

    [Fact]
    public async Task CuotaPorDispositivo_CuartaActivacionDelMismoDispositivo_Denegada()
    {
        var cache = NewCache();
        var limiter = NewLimiter(cache);
        var deviceId = "device-abc";

        // Distintos pacientes y teléfonos: SOLO el dispositivo se comparte,
        // para aislar la cuota por dispositivo en la verificación.
        for (var i = 0; i < 3; i++)
        {
            var patientId = Guid.NewGuid();
            var otherPhone = $"+57301000000{i}";
            await limiter.RegisterAttemptAsync(
                patientId,
                otherPhone,
                deviceId,
                CancellationToken.None
            );
            await cache.RemoveAsync($"sos:ratelimit:patient:{patientId}:cooldown");
        }

        var decision = await limiter.CheckAsync(
            Guid.NewGuid(),
            "+573010000009",
            deviceId,
            CancellationToken.None
        );

        Assert.False(decision.Allowed);
        Assert.Equal("quota-device", decision.Reason);
    }

    [Fact]
    public async Task CacheCaida_FailOpen_PermiteLaEmergencia()
    {
        // (D7 trade-off) Valkey caído: una emergencia real no se bloquea; el
        // hard-guarantee estructural vive en el índice parcial de PostgreSQL.
        var cache = new ThrowingCacheService();
        var limiter = NewLimiter(cache);

        var decision = await limiter.CheckAsync(
            Guid.NewGuid(),
            Phone,
            null,
            CancellationToken.None
        );

        Assert.True(decision.Allowed);
    }

    /// <summary>ICacheService que simula Valkey caído (todas las operaciones lanzan).</summary>
    private sealed class ThrowingCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            where T : class => throw new InvalidOperationException("valkey down");

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
            where T : class => throw new InvalidOperationException("valkey down");

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            throw new InvalidOperationException("valkey down");

        public Task<T> GetOrCreateAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken ct = default
        )
            where T : class => throw new InvalidOperationException("valkey down");
    }
}
