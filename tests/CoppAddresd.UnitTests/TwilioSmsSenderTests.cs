using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Twilio.Exceptions;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests unitarios de <see cref="TwilioSmsSender"/> (F2) con
/// <see cref="FakeTwilioRestClient"/>: sin llamadas reales a Twilio, sin
/// credenciales reales y sin Internet. Cubre también la selección de proveedor
/// por <c>Sms:Provider</c> en el registro DI (fail-soft sin credenciales).
/// </summary>
public sealed class TwilioSmsSenderTests
{
    private const string ValidPhone = "+573053924819";
    private const string FromNumber = "+15765550100";

    private static SmsSettings CreateSettings(
        bool enabled = true,
        string accountSid = "AC_test",
        string authToken = "test-token",
        string fromNumber = FromNumber,
        string messagingServiceSid = "")
        => new()
        {
            Provider = "Twilio",
            IsEnabled = enabled,
            AccountSid = accountSid,
            AuthToken = authToken,
            FromNumber = fromNumber,
            MessagingServiceSid = messagingServiceSid,
        };

    private static TwilioSmsSender CreateSender(
        FakeTwilioRestClient client,
        SmsSettings? settings = null)
        => new(
            Options.Create(settings ?? CreateSettings()),
            client,
            NullLogger<TwilioSmsSender>.Instance);

    private static Twilio.Http.Response JsonResponse(
        System.Net.HttpStatusCode status,
        string json)
        => new(status, json);

    private static string? PostParam(Twilio.Http.Request? request, string key)
        => request?.PostParams
            .FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;

    // =====================================================================
    // SendAsync
    // =====================================================================

    [Fact]
    public async Task SendAsync_SinConfiguracion_NoLlamaATwilioYDevuelveError()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var sender = CreateSender(client, CreateSettings(enabled: false));

        var result = await sender.SendAsync(ValidPhone, "Hola");

        Assert.False(result.Success);
        Assert.Null(result.ProviderMessageId);
        Assert.Contains("no configurado", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(sender.IsConfigured);
    }

    [Fact]
    public async Task SendAsync_SinTelefono_DevuelveErrorSinLlamarATwilio()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new InvalidOperationException("No debe llamarse a Twilio"));
        var sender = CreateSender(client);

        var result = await sender.SendAsync(string.Empty, "Hola");

        Assert.False(result.Success);
        Assert.Contains("teléfono", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_ConFromNumber_EnviaPorMessagesYDevuelveSid()
    {
        Twilio.Http.Request? captured = null;
        var client = new FakeTwilioRestClient(request =>
        {
            captured = request;
            return Task.FromResult(JsonResponse(
                System.Net.HttpStatusCode.Created,
                """{"sid":"SM123","status":"queued","to":"+573053924819"}"""));
        });
        var sender = CreateSender(client);

        var result = await sender.SendAsync(ValidPhone, "Recordatorio de cita");

        Assert.True(result.Success);
        Assert.Equal("SM123", result.ProviderMessageId);
        Assert.Null(result.Error);
        Assert.NotNull(captured);
        Assert.Contains("/Messages.json", captured.Uri.AbsolutePath);
        Assert.Equal(ValidPhone, PostParam(captured, "To"));
        Assert.Equal("Recordatorio de cita", PostParam(captured, "Body"));
        Assert.Equal(FromNumber, PostParam(captured, "From"));
        Assert.Null(PostParam(captured, "MessagingServiceSid"));
    }

    [Fact]
    public async Task SendAsync_ConMessagingServiceSid_UsaElPoolDeTwilio()
    {
        Twilio.Http.Request? captured = null;
        var client = new FakeTwilioRestClient(request =>
        {
            captured = request;
            return Task.FromResult(JsonResponse(
                System.Net.HttpStatusCode.Created,
                """{"sid":"SM456","status":"queued"}"""));
        });
        var sender = CreateSender(
            client,
            CreateSettings(fromNumber: string.Empty, messagingServiceSid: "MG_test"));

        var result = await sender.SendAsync(ValidPhone, "Hola");

        Assert.True(result.Success);
        Assert.Equal("MG_test", PostParam(captured, "MessagingServiceSid"));
        Assert.Null(PostParam(captured, "From"));
    }

    [Fact]
    public async Task SendAsync_ApiExceptionDeTwilio_DevuelveFailedSinLanzar()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiException(21211, 400, "Invalid 'To'", "", null, null));
        var sender = CreateSender(client);

        var result = await sender.SendAsync(ValidPhone, "Hola");

        Assert.False(result.Success);
        Assert.Null(result.ProviderMessageId);
        Assert.Contains("21211", result.Error);
    }

    [Fact]
    public async Task SendAsync_ErrorDeTransporte_DevuelveFailedSinLanzar()
    {
        var client = new FakeTwilioRestClient(
            _ => throw new ApiConnectionException("connection failed"));
        var sender = CreateSender(client);

        var result = await sender.SendAsync(ValidPhone, "Hola");

        Assert.False(result.Success);
        Assert.Contains("transport", result.Error);
    }

    // =====================================================================
    // Selección de proveedor (DI)
    // =====================================================================

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> BaseConfig() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
        ["Cache:Provider"] = "Memory",
        ["Storage:Provider"] = "Local",
    };

    [Fact]
    public void AddInfrastructure_ConTwilioConfigurado_RegistraTwilioSmsSender()
    {
        var config = BaseConfig();
        config["Sms:Provider"] = "Twilio";
        config["Sms:IsEnabled"] = "true";
        config["Sms:AccountSid"] = "AC_test";
        config["Sms:AuthToken"] = "test-token";
        config["Sms:FromNumber"] = FromNumber;

        using var provider = BuildProvider(config);
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

        Assert.IsType<TwilioSmsSender>(sender);
        Assert.True(sender.IsConfigured);
        Assert.Equal("twilio", sender.Provider);
    }

    [Fact]
    public void AddInfrastructure_ConTwilioSinCredenciales_RegistraNoOpSinLanzar()
    {
        var config = BaseConfig();
        config["Sms:Provider"] = "Twilio";
        config["Sms:IsEnabled"] = "false";

        using var provider = BuildProvider(config);
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

        Assert.IsType<NoOpSmsSender>(sender);
        Assert.False(sender.IsConfigured);
        Assert.Equal("noop", sender.Provider);
    }
}
