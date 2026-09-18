using System.Net;
using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Exceptions;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests unitarios de <see cref="OtpService"/> tras la Fase 4: el canal PHONE
/// delega en <see cref="ITwilioOtpService"/> (Twilio Verify V2) y el canal
/// EMAIL conserva el flujo local (hash + salt + auth.otp_codes). Usan SQLite
/// en memoria para el DbContext y fakes para Twilio/tokens/permisos/lookup:
/// sin llamadas reales a Twilio ni PostgreSQL.
/// </summary>
public sealed class OtpServiceTests
{
    private const string Document = "100200300";
    private const string AppCode = "app";
    private const string TestIp = "203.0.113.10";

    // =====================================================================
    // SEND PHONE
    // =====================================================================

    [Theory]
    [InlineData("1", "5765550100", "+15765550100")]
    [InlineData("57", "3001234567", "+573001234567")]
    public async Task SendOtp_Phone_CallsTwilioWithE164AndDoesNotExposeCode(
        string countryCode, string number, string expectedE164)
    {
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: countryCode, phoneNumber: number));

        var (success, error, result) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "phone",
        });

        Assert.True(success, error);
        Assert.Null(error);
        Assert.Equal(1, harness.Twilio.SendCount);
        Assert.Equal(expectedE164, harness.Twilio.LastSendPhone);
        // Nunca se devuelve el código generado por Twilio (ni en Development).
        Assert.Null(result!.DevCode);
        // No se genera OTP local ni se persiste en auth.otp_codes.
        Assert.Equal(0, harness.Db.OtpCodes.Count());
    }

    [Fact]
    public async Task SendOtp_Phone_DoesNotPersistOtpCode()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));

        var (success, _, _) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "phone",
        });

        Assert.True(success);
        Assert.Equal(0, harness.Db.OtpCodes.Count());
    }

    [Fact]
    public async Task SendOtp_Phone_WhenTwilioFails_PropagatesControlledError()
    {
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            twilio => twilio.OnSend = (_, _) =>
                throw new TwilioOtpException(TwilioOtpErrorKind.ProviderError, "Twilio falló", 502, 12400));

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(() =>
            harness.Otp.SendOtpAsync(new SendOtpRequest
            {
                DocumentNumber = Document,
                ContactId = "phone",
            }));

        Assert.Equal(TwilioOtpErrorKind.ProviderError, ex.Kind);
    }

    // =====================================================================
    // VERIFY PHONE
    // =====================================================================

    [Fact]
    public async Task VerifyOtp_Phone_Approved_ProvisionsAndIssuesTokens()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        harness.Twilio.OnCheck = (phone, code, _) =>
            Task.FromResult(new TwilioCheckOtpResult(true, "approved"));

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "123456",
            Application = AppCode,
        });

        Assert.NotNull(result);
        Assert.Equal("fake-access-token", result.AccessToken);
        Assert.Equal("fake-refresh-token", result.RefreshToken);
        Assert.Equal(1, harness.Twilio.CheckCount);
        Assert.Equal("+15765550100", harness.Twilio.LastCheckPhone);
        Assert.Equal("123456", harness.Twilio.LastCheckCode);
        Assert.Equal(1, harness.Tokens.AccessTokenCalls);
        Assert.Equal(1, harness.Tokens.RefreshTokenCalls);
    }

    [Fact]
    public async Task VerifyOtp_Phone_Rejected_DoesNotProvisionOrIssueTokens()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        harness.Twilio.OnCheck = (phone, code, _) =>
            Task.FromResult(new TwilioCheckOtpResult(false, "pending"));

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "000000",
            Application = AppCode,
        });

        Assert.Null(result);
        // No genera JWT ni refresh token.
        Assert.Equal(0, harness.Tokens.AccessTokenCalls);
        Assert.Equal(0, harness.Tokens.RefreshTokenCalls);
        // No aprovisiona usuario.
        Assert.Equal(0, harness.Db.Users.Count());
        Assert.Equal(0, harness.Db.OtpCodes.Count());
    }

    [Fact]
    public async Task VerifyOtp_Phone_WhenRateLimited_PropagatesControlledError()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        harness.Twilio.OnCheck = (_, _, _) =>
            throw new TwilioOtpException(TwilioOtpErrorKind.RateLimited, "límite", 429, 20429);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(() =>
            harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
            {
                DocumentNumber = Document,
                Otp = "123456",
                Application = AppCode,
            }));

        Assert.Equal(TwilioOtpErrorKind.RateLimited, ex.Kind);
        Assert.Equal(0, harness.Tokens.AccessTokenCalls);
    }

    [Fact]
    public async Task VerifyOtp_Phone_WhenProviderUnavailable_PropagatesControlledError()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        harness.Twilio.OnCheck = (_, _, _) =>
            throw new TwilioOtpException(TwilioOtpErrorKind.ProviderUnavailable, "sin conexión");

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(() =>
            harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
            {
                DocumentNumber = Document,
                Otp = "123456",
                Application = AppCode,
            }));

        Assert.Equal(TwilioOtpErrorKind.ProviderUnavailable, ex.Kind);
        Assert.Equal(0, harness.Tokens.AccessTokenCalls);
    }

    // =====================================================================
    // EMAIL (flujo local sin cambios)
    // =====================================================================

    [Fact]
    public async Task SendOtp_Email_StillPersistsOtpCodeAndReturnsDevCodeInDevelopment()
    {
        using var harness = new OtpTestHarness(Patient(email: "paciente@test.com"));

        var (success, error, result) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "email",
        });

        Assert.True(success, error);
        Assert.NotNull(result!.DevCode);
        Assert.Equal(6, result.DevCode.Length);
        Assert.Equal(0, harness.Twilio.SendCount); // EMAIL no toca Twilio

        var otp = Assert.Single(harness.Db.OtpCodes);
        Assert.Equal("Email", otp.Channel);
        Assert.Equal("paciente@test.com", otp.Target);
        Assert.False(string.IsNullOrWhiteSpace(otp.CodeHash));
        Assert.False(string.IsNullOrWhiteSpace(otp.Salt));
        Assert.Null(otp.UsedAt);
    }

    [Fact]
    public async Task VerifyOtp_Email_StillWorksWithLocalHash()
    {
        using var harness = new OtpTestHarness(Patient(email: "paciente@test.com"));
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        await harness.SeedEmailOtpAsync("paciente@test.com", "123456", "salt");

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "123456",
            Application = AppCode,
        });

        Assert.NotNull(result);
        Assert.Equal(0, harness.Twilio.CheckCount); // EMAIL no toca Twilio
        Assert.Equal(1, harness.Tokens.AccessTokenCalls);
        var otp = Assert.Single(harness.Db.OtpCodes);
        Assert.NotNull(otp.UsedAt);
    }

    [Fact]
    public async Task VerifyOtp_Email_WrongCode_ReturnsNullWithoutIssuingTokens()
    {
        using var harness = new OtpTestHarness(Patient(email: "paciente@test.com"));
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        await harness.SeedEmailOtpAsync("paciente@test.com", "123456", "salt");

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "999999",
            Application = AppCode,
        });

        Assert.Null(result);
        Assert.Equal(0, harness.Tokens.AccessTokenCalls);
    }

    // =====================================================================
    // PROTECCIÓN SEND PHONE (integración con IOtpProtectionService)
    // =====================================================================

    [Fact]
    public async Task SendOtp_Phone_Allowed_CallsTwilioAndRegistersSend()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var (success, _, _) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "phone",
        });

        Assert.True(success);
        Assert.Equal(1, harness.Twilio.SendCount);
        Assert.Equal(1, protection.CheckCanSendCalls);
        Assert.Equal(1, protection.RegisterSendCalls);
        Assert.Equal(TestIp, protection.LastSendIp);
        Assert.Equal(Document, protection.LastSendDocument);
        Assert.Equal("+15765550100", protection.LastSendPhone);
    }

    [Fact]
    public async Task SendOtp_Phone_Blocked_ZeroTwilioCalls_NoRegister_Throws429()
    {
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            protection: new FakeOtpProtectionService
            {
                SendResult = OtpProtectionResult.Block(OtpProtectionReason.SendIpRateLimit, 30),
            });
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var ex = await Assert.ThrowsAsync<OtpProtectionException>(() =>
            harness.Otp.SendOtpAsync(new SendOtpRequest
            {
                DocumentNumber = Document,
                ContactId = "phone",
            }));

        Assert.Equal(OtpProtectionReason.SendIpRateLimit, ex.Reason);
        Assert.Equal(30, ex.RetryAfterSeconds);
        Assert.Equal(0, harness.Twilio.SendCount); // Twilio NO llamado
        Assert.Equal(0, protection.RegisterSendCalls);
        Assert.Equal(0, harness.Db.OtpCodes.Count()); // nada persistido
    }

    [Fact]
    public async Task SendOtp_Phone_TwilioFails_DoesNotRegisterSend()
    {
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            twilio => twilio.OnSend = (_, _) =>
                throw new TwilioOtpException(TwilioOtpErrorKind.ProviderUnavailable, "sin conexión"));
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        await Assert.ThrowsAsync<TwilioOtpException>(() =>
            harness.Otp.SendOtpAsync(new SendOtpRequest
            {
                DocumentNumber = Document,
                ContactId = "phone",
            }));

        Assert.Equal(0, protection.RegisterSendCalls); // fallo de Twilio NO consume cuota local
    }

    [Fact]
    public async Task SendOtp_Phone_ProtectionDisabled_NormalBehavior()
    {
        var realProtection = new OtpProtectionService(
            Options.Create(new OtpSecuritySettings { Enabled = false }));
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            protection: realProtection);

        var (success, _, result) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "phone",
        });

        Assert.True(success);
        Assert.Equal(1, harness.Twilio.SendCount);
        Assert.Null(result!.DevCode);
    }

    // =====================================================================
    // PROTECCIÓN VERIFY PHONE (integración con IOtpProtectionService)
    // =====================================================================

    [Fact]
    public async Task VerifyOtp_Phone_Approved_RegistersSucceeded()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        harness.Twilio.OnCheck = (phone, code, _) =>
            Task.FromResult(new TwilioCheckOtpResult(true, "approved"));
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "123456",
            Application = AppCode,
        });

        Assert.NotNull(result);
        Assert.Equal(1, protection.RegisterVerifySucceededCalls);
        Assert.Equal(0, protection.RegisterVerifyFailedCalls);
        Assert.Equal("+15765550100", protection.LastVerifyPhone);
    }

    [Fact]
    public async Task VerifyOtp_Phone_Rejected_RegistersFailed()
    {
        using var harness = new OtpTestHarness(Patient(phoneCountryCode: "1", phoneNumber: "5765550100"));
        harness.Twilio.OnCheck = (phone, code, _) =>
            Task.FromResult(new TwilioCheckOtpResult(false, "pending"));
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "000000",
            Application = AppCode,
        });

        Assert.Null(result); // mismo resultado funcional actual (código inválido)
        Assert.Equal(1, protection.RegisterVerifyFailedCalls);
        Assert.Equal(0, protection.RegisterVerifySucceededCalls);
    }

    [Fact]
    public async Task VerifyOtp_Phone_Blocked_ZeroTwilioCalls_NoJwtNoProvisioning()
    {
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            protection: new FakeOtpProtectionService
            {
                VerifyResult = OtpProtectionResult.Block(OtpProtectionReason.VerifyPhoneLocked, 120),
            });

        var ex = await Assert.ThrowsAsync<OtpProtectionException>(() =>
            harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
            {
                DocumentNumber = Document,
                Otp = "123456",
                Application = AppCode,
            }));

        Assert.Equal(OtpProtectionReason.VerifyPhoneLocked, ex.Reason);
        Assert.Equal(120, ex.RetryAfterSeconds);
        Assert.Equal(0, harness.Twilio.CheckCount); // Twilio NO llamado
        Assert.Equal(0, harness.Tokens.AccessTokenCalls);
        Assert.Equal(0, harness.Tokens.RefreshTokenCalls);
        Assert.Equal(0, harness.Db.Users.Count()); // sin aprovisionamiento
    }

    [Fact]
    public async Task VerifyOtp_Phone_ProtectionDisabled_NormalBehavior()
    {
        var realProtection = new OtpProtectionService(
            Options.Create(new OtpSecuritySettings { Enabled = false }));
        using var harness = new OtpTestHarness(
            Patient(phoneCountryCode: "1", phoneNumber: "5765550100"),
            protection: realProtection);
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        harness.Twilio.OnCheck = (phone, code, _) =>
            Task.FromResult(new TwilioCheckOtpResult(true, "approved"));

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "123456",
            Application = AppCode,
        });

        Assert.NotNull(result);
        Assert.Equal(1, harness.Tokens.AccessTokenCalls);
    }

    // =====================================================================
    // EMAIL: NO usa el motor de protección ni Twilio
    // =====================================================================

    [Fact]
    public async Task SendOtp_Email_DoesNotUseProtectionOrTwilio()
    {
        using var harness = new OtpTestHarness(Patient(email: "paciente@test.com"));
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var (success, _, result) = await harness.Otp.SendOtpAsync(new SendOtpRequest
        {
            DocumentNumber = Document,
            ContactId = "email",
        });

        Assert.True(success);
        Assert.NotNull(result!.DevCode);
        Assert.Equal(0, protection.CheckCanSendCalls);
        Assert.Equal(0, protection.RegisterSendCalls);
        Assert.Equal(0, harness.Twilio.SendCount);
        Assert.Equal(1, harness.Db.OtpCodes.Count()); // flujo local intacto
    }

    [Fact]
    public async Task VerifyOtp_Email_DoesNotUseProtectionOrTwilio()
    {
        using var harness = new OtpTestHarness(Patient(email: "paciente@test.com"));
        var userId = await harness.SeedUserAsync();
        await harness.SeedApplicationAsync();
        harness.PatientLookup.Result = harness.PatientLookup.Result! with { UserId = userId };
        await harness.SeedEmailOtpAsync("paciente@test.com", "123456", "salt");
        var protection = Assert.IsType<FakeOtpProtectionService>(harness.Protection);

        var result = await harness.Otp.VerifyOtpAsync(new VerifyOtpRequest
        {
            DocumentNumber = Document,
            Otp = "123456",
            Application = AppCode,
        });

        Assert.NotNull(result);
        Assert.Equal(0, protection.CheckCanVerifyCalls);
        Assert.Equal(0, protection.RegisterVerifyFailedCalls);
        Assert.Equal(0, protection.RegisterVerifySucceededCalls);
        Assert.Equal(0, harness.Twilio.CheckCount);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static PatientLookupResult Patient(
        string? email = null,
        string? phoneCountryCode = null,
        string? phoneNumber = null)
        => new(
            Id: Guid.NewGuid(),
            UserId: null,
            FirstName: "Ana",
            LastName: "Paciente",
            DocumentNumber: Document,
            Email: email,
            PhoneCountryCode: phoneCountryCode,
            PhoneNumber: phoneNumber);

    // =====================================================================
    // Fakes
    // =====================================================================

    private sealed class FakeTwilioOtpService : ITwilioOtpService
    {
        public int SendCount { get; private set; }
        public int CheckCount { get; private set; }
        public string? LastSendPhone { get; private set; }
        public string? LastCheckPhone { get; private set; }
        public string? LastCheckCode { get; private set; }

        public Func<string, CancellationToken, Task<TwilioSendOtpResult>> OnSend { get; set; } =
            (phone, ct) => Task.FromResult(new TwilioSendOtpResult("VE123", "pending"));

        public Func<string, string, CancellationToken, Task<TwilioCheckOtpResult>> OnCheck { get; set; } =
            (phone, code, ct) => Task.FromResult(new TwilioCheckOtpResult(true, "approved"));

        public Task<TwilioSendOtpResult> SendAsync(string phoneNumber, CancellationToken ct = default)
        {
            SendCount++;
            LastSendPhone = phoneNumber;
            return OnSend(phoneNumber, ct);
        }

        public Task<TwilioCheckOtpResult> CheckAsync(string phoneNumber, string code, CancellationToken ct = default)
        {
            CheckCount++;
            LastCheckPhone = phoneNumber;
            LastCheckCode = code;
            return OnCheck(phoneNumber, code, ct);
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public int AccessTokenCalls { get; private set; }
        public int RefreshTokenCalls { get; private set; }

        public string GenerateAccessToken(
            ApplicationUser user, IEnumerable<string> roles, string audience, IEnumerable<string> permissions, long applicationSessionVersion = 0)
        {
            AccessTokenCalls++;
            return "fake-access-token";
        }

        public Task<string> GenerateRefreshTokenAsync(Guid userId, Guid? applicationId, CancellationToken ct = default, long applicationSessionVersion = 0)
        {
            RefreshTokenCalls++;
            return Task.FromResult("fake-refresh-token");
        }
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<IEnumerable<PermissionResponse>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<PermissionResponse>());

        public Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<PermissionResponse?>(null);

        public Task<PermissionResponse?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult<PermissionResponse?>(null);

        public Task<IEnumerable<PermissionResponse>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<PermissionResponse>());

        public Task<IEnumerable<PermissionResponse>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<PermissionResponse>());

        public Task<IEnumerable<string>> GetUserAllPermissionCodesAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<string>());

        public Task<IEnumerable<string>> GetUserEffectivePermissionCodesAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Enumerable.Empty<string>());

        public Task<(bool Success, string? Error)> AssignToRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
            => Task.FromResult((false, (string?)"no implementado"));

        public Task<(bool Success, string? Error)> RemoveFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
            => Task.FromResult((false, (string?)"no implementado"));

        public Task<(bool Success, string? Error)> SetForRoleAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct = default)
            => Task.FromResult((false, (string?)"no implementado"));

        public Task<(bool Success, string? Error)> AssignToUserAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
            => Task.FromResult((false, (string?)"no implementado"));

        public Task<(bool Success, string? Error)> RemoveFromUserAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
            => Task.FromResult((false, (string?)"no implementado"));

        public Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class FakePatientLookup : IPatientLookupService
    {
        public PatientLookupResult? Result { get; set; }

        public Task<PatientLookupResult?> FindByDocumentNumberAsync(
            string documentNumber, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    private sealed class FakeOtpProtectionService : IOtpProtectionService
    {
        public OtpProtectionResult SendResult { get; set; } = OtpProtectionResult.Allow();
        public OtpProtectionResult VerifyResult { get; set; } = OtpProtectionResult.Allow();

        public int CheckCanSendCalls { get; private set; }
        public int RegisterSendCalls { get; private set; }
        public int CheckCanVerifyCalls { get; private set; }
        public int RegisterVerifyFailedCalls { get; private set; }
        public int RegisterVerifySucceededCalls { get; private set; }

        public string? LastSendIp { get; private set; }
        public string? LastSendDocument { get; private set; }
        public string? LastSendPhone { get; private set; }
        public string? LastVerifyIp { get; private set; }
        public string? LastVerifyPhone { get; private set; }

        public OtpProtectionResult CheckCanSend(string ipAddress, string documentNumber, string phoneE164)
        {
            CheckCanSendCalls++;
            LastSendIp = ipAddress;
            LastSendDocument = documentNumber;
            LastSendPhone = phoneE164;
            return SendResult;
        }

        public void RegisterSend(string ipAddress, string documentNumber, string phoneE164)
        {
            RegisterSendCalls++;
            LastSendIp = ipAddress;
            LastSendDocument = documentNumber;
            LastSendPhone = phoneE164;
        }

        public OtpProtectionResult CheckCanVerify(string ipAddress, string phoneE164)
        {
            CheckCanVerifyCalls++;
            LastVerifyIp = ipAddress;
            LastVerifyPhone = phoneE164;
            return VerifyResult;
        }

        public void RegisterVerifyFailed(string ipAddress, string phoneE164)
        {
            RegisterVerifyFailedCalls++;
            LastVerifyIp = ipAddress;
            LastVerifyPhone = phoneE164;
        }

        public void RegisterVerifySucceeded(string ipAddress, string phoneE164)
        {
            RegisterVerifySucceededCalls++;
            LastVerifyIp = ipAddress;
            LastVerifyPhone = phoneE164;
        }
    }

    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public FakeHttpContextAccessor(string ipAddress)
        {
            HttpContext = new DefaultHttpContext();
            HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        }

        public HttpContext? HttpContext { get; set; }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CoppAddresd.Auth.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // =====================================================================
    // Harness (SQLite in-memory + DI)
    // =====================================================================

    private sealed class OtpTestHarness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public OtpTestHarness(
            PatientLookupResult? patient,
            Action<FakeTwilioOtpService>? configureTwilio = null,
            IOtpProtectionService? protection = null)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddSingleton<IHostEnvironment>(new TestEnvironment { EnvironmentName = "Development" });
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_connection));

            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<AuthDbContext>()
                .AddDefaultTokenProviders();

            Twilio = new FakeTwilioOtpService();
            configureTwilio?.Invoke(Twilio);
            services.AddSingleton<ITwilioOtpService>(Twilio);

            Protection = protection ?? new FakeOtpProtectionService();
            services.AddSingleton<IOtpProtectionService>(Protection);
            services.AddSingleton<IHttpContextAccessor>(new FakeHttpContextAccessor(TestIp));

            PatientLookup = new FakePatientLookup { Result = patient };
            services.AddSingleton<IPatientLookupService>(PatientLookup);

            Tokens = new FakeTokenService();
            services.AddSingleton<ITokenService>(Tokens);

            services.AddSingleton<IPermissionService>(new FakePermissionService());

            services.AddSingleton(Options.Create(new JwtSettings
            {
                Secret = "test-secret-key-at-least-32-characters-long",
                Issuer = "CoppAddresd.Auth.Tests",
                ValidAudiences = new List<string> { "app", "erp" },
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 7,
            }));

            services.AddScoped<OtpService>();

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();

            Db.Database.EnsureCreated();
        }

        public FakeTwilioOtpService Twilio { get; }
        public FakeTokenService Tokens { get; }
        public FakePatientLookup PatientLookup { get; }
        public IOtpProtectionService Protection { get; }

        public AuthDbContext Db => _scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        public OtpService Otp => _scope.ServiceProvider.GetRequiredService<OtpService>();

        public async Task<Guid> SeedUserAsync()
        {
            var user = new ApplicationUser
            {
                UserName = "paciente@test.com",
                Email = "paciente@test.com",
                FirstName = "Ana",
                LastName = "Paciente",
                EmailConfirmed = true,
            };
            var result = await _scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>()
                .CreateAsync(user);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
            return user.Id;
        }

        public async Task SeedApplicationAsync()
        {
            // Calificado: la rama dev agregó el namespace CoppAddresd.Application,
            // que colisiona con la entidad CoppAddresd.Auth.Entities.Application.
            Db.Applications.Add(new CoppAddresd.Auth.Entities.Application
            {
                Id = Guid.NewGuid(),
                Code = AppCode,
                Name = "App",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await Db.SaveChangesAsync();
        }

        public async Task SeedEmailOtpAsync(string email, string code, string salt)
        {
            Db.OtpCodes.Add(new OtpCode
            {
                Id = Guid.NewGuid(),
                DocumentNumber = Document,
                Channel = "Email",
                Target = email,
                CodeHash = HashOtp(salt, code),
                Salt = salt,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
            });
            await Db.SaveChangesAsync();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            _connection.Dispose();
        }

        private static string HashOtp(string salt, string code)
        {
            var bytes = Encoding.UTF8.GetBytes(salt + code.Trim());
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
    }
}
