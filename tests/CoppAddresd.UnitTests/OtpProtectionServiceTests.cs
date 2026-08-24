using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Services;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests unitarios de <see cref="OtpProtectionService"/>. Usan un reloj fake
/// determinista para comprobar cooldown, ventanas y lockout sin esperar tiempo
/// real. No hay llamadas a Twilio, PostgreSQL ni endpoints HTTP.
/// </summary>
public sealed class OtpProtectionServiceTests
{
    private const string Ip = "203.0.113.10";
    private const string Doc = "DOC123";
    private const string Phone = "+573000000000";

    // =====================================================================
    // Protección deshabilitada
    // =====================================================================

    [Fact]
    public void Disabled_AllowsSendRepeatedly_WithoutBlocking()
    {
        var (svc, _) = Create(s => s.Enabled = false);

        for (var i = 0; i < 50; i++)
        {
            svc.RegisterSend(Ip, Doc, Phone);
            Assert.True(svc.CheckCanSend(Ip, Doc, Phone).Allowed);
        }
    }

    [Fact]
    public void Disabled_AllowsVerifyRepeatedly_WithoutLockout()
    {
        var (svc, _) = Create(s => s.Enabled = false);

        for (var i = 0; i < 50; i++)
        {
            svc.RegisterVerifyFailed(Ip, Phone);
            Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed);
        }
    }

    // =====================================================================
    // SEND
    // =====================================================================

    [Fact]
    public void Send_FirstCall_Allowed()
    {
        var (svc, _) = Create();

        var r = svc.CheckCanSend(Ip, Doc, Phone);

        Assert.True(r.Allowed);
        Assert.Null(r.Reason);
        Assert.Null(r.RetryAfterSeconds);
    }

    [Fact]
    public void Send_RegisterSend_ConsumesQuota()
    {
        var (svc, _) = Create();
        for (var i = 0; i < 5; i++)
        {
            svc.RegisterSend(Ip, Doc, Phone);
        }

        var r = svc.CheckCanSend(Ip, Doc, Phone);

        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendIpRateLimit, r.Reason);
    }

    [Fact]
    public void Send_CheckWithoutRegister_DoesNotConsumeQuota()
    {
        var (svc, _) = Create();
        for (var i = 0; i < 100; i++)
        {
            Assert.True(svc.CheckCanSend(Ip, Doc, Phone).Allowed);
        }
    }

    [Fact]
    public void Send_IpPerHour_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 2;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendIpRateLimit, r.Reason);
    }

    [Fact]
    public void Send_PhonePerMinute_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 2;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendPhoneRateLimit, r.Reason);
    }

    [Fact]
    public void Send_PhonePerHour_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 2;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendPhoneRateLimit, r.Reason);
    }

    [Fact]
    public void Send_PhonePerDay_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 2;
            s.SendPerDocumentPerHour = 1_000;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendPhoneRateLimit, r.Reason);
    }

    [Fact]
    public void Send_Cooldown_BlocksAndReportsRetryAfter()
    {
        var (svc, clock) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
            s.SendPhoneCooldownSeconds = 60;
        });

        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendPhoneCooldown, r.Reason);
        Assert.NotNull(r.RetryAfterSeconds);
        Assert.InRange(r.RetryAfterSeconds!.Value, 1, 60);

        clock.Advance(TimeSpan.FromSeconds(61));

        Assert.True(svc.CheckCanSend(Ip, Doc, Phone).Allowed);
    }

    [Fact]
    public void Send_DocumentPerHour_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPhoneCooldownSeconds = 0;
            s.SendPerDocumentPerHour = 2;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);

        var r = svc.CheckCanSend(Ip, Doc, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.SendDocumentRateLimit, r.Reason);
    }

    // =====================================================================
    // VERIFY
    // =====================================================================

    [Fact]
    public void Verify_FirstAttempt_Allowed()
    {
        var (svc, _) = Create();

        Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed);
    }

    [Fact]
    public void Verify_IpPerMinute_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.VerifyPerIpPerMinute = 2;
            s.VerifyPerPhoneLimit = 1_000;
            s.VerifyPhoneMaxFailedAttempts = 1_000;
        });

        svc.RegisterVerifySucceeded(Ip, Phone);
        svc.RegisterVerifySucceeded(Ip, Phone);

        var r = svc.CheckCanVerify(Ip, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.VerifyIpRateLimit, r.Reason);
    }

    [Fact]
    public void Verify_PhoneWindow_Blocks()
    {
        var (svc, _) = Create(s =>
        {
            s.VerifyPerIpPerMinute = 1_000;
            s.VerifyPerPhoneLimit = 2;
            s.VerifyPhoneMaxFailedAttempts = 1_000;
        });

        svc.RegisterVerifySucceeded(Ip, Phone);
        svc.RegisterVerifySucceeded(Ip, Phone);

        var r = svc.CheckCanVerify(Ip, Phone);
        Assert.False(r.Allowed);
        Assert.Equal(OtpProtectionReason.VerifyPhoneRateLimit, r.Reason);
    }

    [Fact]
    public void Verify_FailedAttempts_TriggerLockout()
    {
        var (svc, clock) = Create(s =>
        {
            s.VerifyPerIpPerMinute = 1_000;
            s.VerifyPerPhoneLimit = 1_000;
            s.VerifyPhoneMaxFailedAttempts = 3;
            s.VerifyPhoneLockoutSeconds = 300;
        });

        // 2 fallos: aún permitido.
        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifyFailed(Ip, Phone);
        Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed);

        // 3er fallo: activa lockout.
        svc.RegisterVerifyFailed(Ip, Phone);
        var blocked = svc.CheckCanVerify(Ip, Phone);
        Assert.False(blocked.Allowed);
        Assert.Equal(OtpProtectionReason.VerifyPhoneLocked, blocked.Reason);
        Assert.NotNull(blocked.RetryAfterSeconds);
        Assert.InRange(blocked.RetryAfterSeconds!.Value, 1, 300);

        // Durante el lockout se sigue bloqueando.
        clock.Advance(TimeSpan.FromSeconds(299));
        Assert.False(svc.CheckCanVerify(Ip, Phone).Allowed);

        // Tras expirar el lockout, vuelve a permitirse.
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed);
    }

    [Fact]
    public void Verify_Successful_ClearsFailures()
    {
        var (svc, _) = Create(s =>
        {
            s.VerifyPerIpPerMinute = 1_000;
            s.VerifyPerPhoneLimit = 1_000;
            s.VerifyPhoneMaxFailedAttempts = 3;
            s.VerifyPhoneLockoutSeconds = 300;
        });

        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifySucceeded(Ip, Phone); // limpia fallos

        // Tras el éxito, se requieren 3 fallos nuevos para volver a bloquear.
        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifyFailed(Ip, Phone);
        Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed);
    }

    [Fact]
    public void Verify_Successful_RemovesLockout()
    {
        var (svc, _) = Create(s =>
        {
            s.VerifyPerIpPerMinute = 1_000;
            s.VerifyPerPhoneLimit = 1_000;
            s.VerifyPhoneMaxFailedAttempts = 3;
            s.VerifyPhoneLockoutSeconds = 300;
        });

        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifyFailed(Ip, Phone);
        svc.RegisterVerifyFailed(Ip, Phone);
        Assert.False(svc.CheckCanVerify(Ip, Phone).Allowed); // lockout activo

        svc.RegisterVerifySucceeded(Ip, Phone);

        Assert.True(svc.CheckCanVerify(Ip, Phone).Allowed); // lockout eliminado
    }

    // =====================================================================
    // Limpieza
    // =====================================================================

    [Fact]
    public void Cleanup_ExpiredWindow_StopsBlocking()
    {
        var (svc, clock) = Create(s =>
        {
            s.SendPerIpPerMinute = 2;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
            s.SendPhoneCooldownSeconds = 0;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        svc.RegisterSend(Ip, Doc, Phone);
        Assert.False(svc.CheckCanSend(Ip, Doc, Phone).Allowed);

        clock.Advance(TimeSpan.FromSeconds(61)); // la ventana de 1 minuto expiró

        Assert.True(svc.CheckCanSend(Ip, Doc, Phone).Allowed);
    }

    [Fact]
    public void Cleanup_DoesNotRemoveActiveStates()
    {
        var (svc, clock) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000;
            s.SendPerIpPerHour = 1_000;
            s.SendPerPhonePerMinute = 1_000;
            s.SendPerPhonePerHour = 1_000;
            s.SendPerPhonePerDay = 1_000;
            s.SendPerDocumentPerHour = 1_000;
            s.SendPhoneCooldownSeconds = 60;
        });

        svc.RegisterSend(Ip, Doc, Phone);
        clock.Advance(TimeSpan.FromSeconds(30));

        // Varias operaciones disparan la limpieza oportunista; el cooldown
        // activo no debe perderse.
        for (var i = 0; i < 10; i++)
        {
            Assert.False(svc.CheckCanSend(Ip, Doc, Phone).Allowed);
            Assert.Equal(OtpProtectionReason.SendPhoneCooldown, svc.CheckCanSend(Ip, Doc, Phone).Reason);
        }
    }

    // =====================================================================
    // Concurrencia
    // =====================================================================

    [Fact]
    public void ConcurrentAccess_DoesNotThrow()
    {
        var (svc, _) = Create(s =>
        {
            s.SendPerIpPerMinute = 1_000_000;
            s.SendPerIpPerHour = 1_000_000;
            s.SendPerPhonePerMinute = 1_000_000;
            s.SendPerPhonePerHour = 1_000_000;
            s.SendPerPhonePerDay = 1_000_000;
            s.SendPerDocumentPerHour = 1_000_000;
            s.VerifyPerIpPerMinute = 1_000_000;
            s.VerifyPerPhoneLimit = 1_000_000;
            s.VerifyPhoneMaxFailedAttempts = 1_000_000;
        });

        Parallel.For(0, 1_000, _ =>
        {
            svc.RegisterSend(Ip, Doc, Phone);
            svc.CheckCanSend(Ip, Doc, Phone);
            svc.RegisterVerifyFailed(Ip, Phone);
            svc.CheckCanVerify(Ip, Phone);
            svc.RegisterVerifySucceeded(Ip, Phone);
        });
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static (OtpProtectionService Service, FakeTimeProvider Clock) Create(
        Action<OtpSecuritySettings>? configure = null)
    {
        var settings = new OtpSecuritySettings();
        configure?.Invoke(settings);

        var clock = new FakeTimeProvider
        {
            Now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var service = new OtpProtectionService(Options.Create(settings), clock);
        return (service, clock);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan delta) => Now = Now.Add(delta);
    }
}
