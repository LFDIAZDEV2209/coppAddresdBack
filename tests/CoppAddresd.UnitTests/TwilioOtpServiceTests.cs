using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Exceptions;
using CoppAddresd.Auth.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twilio.Exceptions;
using Twilio.Http;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests unitarios de <see cref="TwilioOtpService"/> usando
/// <see cref="FakeTwilioRestClient"/>: sin llamadas reales a Twilio, sin
/// credenciales reales y sin dependencia de Internet.
/// </summary>
public sealed class TwilioOtpServiceTests
{
    private const string ValidPhone = "+15765550100";
    private const string ServiceSid = "VA_testservice";

    private static TwilioSettings CreateSettings(bool enabled = true) => new()
    {
        IsEnabled = enabled,
        AccountSid = "AC_test",
        ApiKeySid = "SK_test",
        ApiKeySecret = "test-secret",
        VerifyServiceSid = ServiceSid,
    };

    private static TwilioOtpService CreateService(
        FakeTwilioRestClient client,
        TwilioSettings? settings = null)
    {
        return new TwilioOtpService(
            Options.Create(settings ?? CreateSettings()),
            client,
            NullLogger<TwilioOtpService>.Instance);
    }

    private static Twilio.Http.Response JsonResponse(
        System.Net.HttpStatusCode status,
        string json)
        => new(status, json);

    // =====================================================================
    // SendAsync
    // =====================================================================

    [Fact]
    public async Task SendAsync_WhenDisabled_ThrowsDisabledError()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client, CreateSettings(enabled: false));

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.SendAsync(ValidPhone));

        Assert.Equal(TwilioOtpErrorKind.Disabled, ex.Kind);
    }

    [Fact]
    public async Task SendAsync_WhenPhoneEmpty_ThrowsInvalidPhone()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.SendAsync(string.Empty));

        Assert.Equal(TwilioOtpErrorKind.InvalidPhone, ex.Kind);
    }

    [Theory]
    [InlineData("5765550100")]            // sin '+'
    [InlineData("+1 5765550100")]         // con espacio
    [InlineData("+123")]                  // demasiado corto (<7 dígitos E.164)
    [InlineData("+05765550100")]          // código de país con 0 inicial (inválido)
    [InlineData("+157655501001234567")]   // demasiado largo (>15 dígitos E.164)
    public async Task SendAsync_WhenPhoneInvalid_ThrowsInvalidPhone(string phone)
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.SendAsync(phone));

        Assert.Equal(TwilioOtpErrorKind.InvalidPhone, ex.Kind);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_ReturnsPendingAndUsesSmsChannel()
    {
        Twilio.Http.Request? captured = null;
        var client = new FakeTwilioRestClient(async request =>
        {
            captured = request;
            return JsonResponse(System.Net.HttpStatusCode.Created,
                """{"sid":"VE123","status":"pending","valid":false,"to":"+15765550100"}""");
        });
        var service = CreateService(client);

        var result = await service.SendAsync(ValidPhone);

        Assert.Equal("VE123", result.VerificationSid);
        Assert.Equal("pending", result.Status);
        Assert.NotNull(captured);
        Assert.Contains(ServiceSid, captured.Uri.AbsolutePath);
        var channel = PostParam(captured, "Channel");
        Assert.NotNull(channel);
        Assert.Equal("sms", channel);
        Assert.Equal(ValidPhone, PostParam(captured, "To"));
    }

    [Fact]
    public async Task SendAsync_WhenProviderReturnsApiError_MapsToTypedException()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiException(60200, 400, "Invalid parameter", "", null, null));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.SendAsync(ValidPhone));

        Assert.Equal(TwilioOtpErrorKind.InvalidPhone, ex.Kind);
        Assert.Equal(400, ex.TwilioStatusCode);
        Assert.Equal(60200, ex.TwilioCode);
    }

    [Fact]
    public async Task SendAsync_WhenProviderUnreachable_MapsToProviderUnavailable()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiConnectionException("connection failed"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.SendAsync(ValidPhone));

        Assert.Equal(TwilioOtpErrorKind.ProviderUnavailable, ex.Kind);
    }

    // =====================================================================
    // CheckAsync
    // =====================================================================

    [Fact]
    public async Task CheckAsync_WhenDisabled_ThrowsDisabledError()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client, CreateSettings(enabled: false));

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.CheckAsync(ValidPhone, "123456"));

        Assert.Equal(TwilioOtpErrorKind.Disabled, ex.Kind);
    }

    [Fact]
    public async Task CheckAsync_WhenPhoneEmpty_ThrowsInvalidPhone()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.CheckAsync(string.Empty, "123456"));

        Assert.Equal(TwilioOtpErrorKind.InvalidPhone, ex.Kind);
    }

    [Fact]
    public async Task CheckAsync_WhenCodeEmpty_ThrowsInvalidParameter()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.CheckAsync(ValidPhone, string.Empty));

        Assert.Equal(TwilioOtpErrorKind.InvalidParameter, ex.Kind);
    }

    [Fact]
    public async Task CheckAsync_WhenWrongCode_ReturnsNotApprovedWithoutThrowing()
    {
        Twilio.Http.Request? captured = null;
        var client = new FakeTwilioRestClient(async request =>
        {
            captured = request;
            return JsonResponse(System.Net.HttpStatusCode.OK,
                """{"sid":"VE123","status":"pending","valid":false,"to":"+15765550100"}""");
        });
        var service = CreateService(client);

        var result = await service.CheckAsync(ValidPhone, "000000");

        Assert.False(result.IsApproved);
        Assert.Equal("pending", result.Status);
        Assert.Equal("000000", PostParam(captured, "Code"));
        Assert.Equal(ValidPhone, PostParam(captured, "To"));
    }

    [Fact]
    public async Task CheckAsync_WhenApproved_ReturnsApproved()
    {
        var client = new FakeTwilioRestClient(_ => Task.FromResult(
            JsonResponse(System.Net.HttpStatusCode.OK,
                """{"sid":"VE123","status":"approved","valid":true,"to":"+15765550100"}""")));
        var service = CreateService(client);

        var result = await service.CheckAsync(ValidPhone, "123456");

        Assert.True(result.IsApproved);
        Assert.Equal("approved", result.Status);
    }

    [Fact]
    public async Task CheckAsync_WhenStatusPending_ReturnsNotApproved()
    {
        var client = new FakeTwilioRestClient(_ => Task.FromResult(
            JsonResponse(System.Net.HttpStatusCode.OK,
                """{"sid":"VE123","status":"pending","valid":false,"to":"+15765550100"}""")));
        var service = CreateService(client);

        var result = await service.CheckAsync(ValidPhone, "654321");

        Assert.False(result.IsApproved);
    }

    [Fact]
    public async Task CheckAsync_WhenProviderRateLimits_MapsToRateLimited()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiException(20429, 429, "Too Many Requests", "", null, null));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.CheckAsync(ValidPhone, "123456"));

        Assert.Equal(TwilioOtpErrorKind.RateLimited, ex.Kind);
        Assert.Equal(429, ex.TwilioStatusCode);
        Assert.Equal(20429, ex.TwilioCode);
    }

    [Fact]
    public async Task CheckAsync_WhenProviderUnreachable_MapsToProviderUnavailable()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiConnectionException("connection failed"));
        var service = CreateService(client);

        var ex = await Assert.ThrowsAsync<TwilioOtpException>(
            () => service.CheckAsync(ValidPhone, "123456"));

        Assert.Equal(TwilioOtpErrorKind.ProviderUnavailable, ex.Kind);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static string? PostParam(Twilio.Http.Request? request, string key)
    {
        return request?.PostParams
            .FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
