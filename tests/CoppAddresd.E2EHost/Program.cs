extern alias ApiEntry;

using System.Text.Json;
using Amazon.GeoPlaces;
using Amazon.GeoPlaces.Model;
using Amazon.Runtime;
using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Common;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Host E2E exclusivo de tests: API, autenticación y PostgreSQL reales;
// únicamente se sustituyen los transportes externos de emergencia.
var directory = Environment.GetEnvironmentVariable("COP_E2E_OUTPUT")
    ?? throw new InvalidOperationException("COP_E2E_OUTPUT debe apuntar a un directorio temporal.");
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "";
if (!connection.Contains("Database=copp_review_", StringComparison.Ordinal))
    throw new InvalidOperationException("El host E2E requiere una base copp_review_ aislada.");
if (!Path.GetFullPath(directory).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
    throw new InvalidOperationException("La evidencia E2E debe quedar fuera del repositorio.");
Directory.CreateDirectory(directory);
await using var factory = new TestApiFactory(directory);
factory.UseKestrel(options => options.ListenLocalhost(5122));
using var client = factory.CreateClient();
Console.WriteLine("API E2E lista: transportes de emergencia aislados; PostgreSQL real.");
await Task.Delay(Timeout.Infinite);

sealed class TestApiFactory(string output) : WebApplicationFactory<ApiEntry::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Review");
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CoppAddresd.Api")));
        builder.ConfigureTestServices(services =>
        {
            if (Environment.GetEnvironmentVariable("COP_E2E_GEOCODING") == "synthetic")
            {
                services.RemoveAll<IAmazonGeoPlaces>();
                services.AddSingleton<IAmazonGeoPlaces>(new SyntheticPlaces(output));
            }
            services.RemoveAll<ISmsSender>(); services.RemoveAll<IVoiceCaller>(); services.RemoveAll<IEmailService>();
            services.AddSingleton(new EmergencyTransport(output));
            // Ejercita también el sender productivo y el formulario Twilio real;
            // solo se sustituye el límite de red, nunca se usan credenciales reales.
            services.RemoveAll<ITwilioRestClient>();
            services.AddSingleton<ITwilioRestClient>(sp => new IsolatedSmsClient(output, sp.GetRequiredService<EmergencyTransport>()));
            services.AddSingleton(Options.Create(new SmsSettings
            {
                Provider = "Twilio", IsEnabled = true, AccountSid = "AC" + new string('0', 32),
                AuthToken = "isolated-sms-only", FromNumber = "+12025550199",
            }));
            services.AddScoped<ISmsSender, TwilioSmsSender>();
            services.AddSingleton<IVoiceCaller>(sp => sp.GetRequiredService<EmergencyTransport>());
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<EmergencyTransport>());
        });
    }
}

sealed class IsolatedSmsClient(string output, EmergencyTransport transport) : ITwilioRestClient
{
    public string AccountSid => "AC" + new string('0', 32);
    public string Region => null!;
    public Twilio.Http.HttpClient HttpClient => null!;
    public Twilio.Http.Response Request(Twilio.Http.Request request) => throw new NotSupportedException();
    public async Task<Twilio.Http.Response> RequestAsync(Twilio.Http.Request request)
    {
        var form = request.PostParams.ToDictionary(p => p.Key, p => p.Value);
        if (request.Uri.Host != "api.twilio.com" || request.Uri.AbsolutePath != $"/2010-04-01/Accounts/{AccountSid}/Messages.json"
            || request.Method != Twilio.Http.HttpMethod.Post || form["From"] != "+12025550199"
            || form.Keys.Any(k => k is not ("To" or "From" or "Body" or "StatusCallback"))
            || form["To"] is not ("+12025550101" or "+12025550102"))
            throw new InvalidOperationException("SMS fuera del contrato de QA; no existe transporte de red.");
        await File.AppendAllTextAsync(Path.Combine(output, "sms-wire-ledger.jsonl"),
            JsonSerializer.Serialize(new { endpoint = request.Uri.AbsoluteUri, method = "POST", fields = form.Keys.Order().ToArray(), from = form["From"], at = DateTime.UtcNow }) + "\n");
        var result = await transport.SendAsync(form["To"], form["Body"], statusCallbackUrl: form.GetValueOrDefault("StatusCallback"));
        return result.Success
            ? new Twilio.Http.Response(System.Net.HttpStatusCode.Created, JsonSerializer.Serialize(new { sid = result.ProviderMessageId, status = "queued" }))
            : new Twilio.Http.Response(System.Net.HttpStatusCode.BadRequest, "{\"code\":21211,\"message\":\"Synthetic rejected destination\",\"status\":400}");
    }
}

sealed class EmergencyTransport(string output) : ISmsSender, IVoiceCaller, IEmailService
{
    private readonly SemaphoreSlim gate = new(1);
    public string Provider => "e2e-only";
    public bool IsConfigured => true;
    private async Task RecordAsync(string channel, string destination, string? callback, CancellationToken ct, string? body = null)
    {
        await gate.WaitAsync(ct);
        try { await File.AppendAllTextAsync(Path.Combine(output, "emergency-ledger.jsonl"),
            JsonSerializer.Serialize(new { channel, destination, callback, body, at = DateTime.UtcNow }) + "\n", ct); }
        finally { gate.Release(); }
    }
    public async Task<SmsSendResult> SendAsync(string phoneNumber, string body, CancellationToken ct = default, string? statusCallbackUrl = null)
    {
        await RecordAsync("sms", phoneNumber, statusCallbackUrl, ct, body);
        return File.Exists(Path.Combine(output, "fail-primary")) && phoneNumber.EndsWith("0101", StringComparison.Ordinal)
            ? new(false, null, "twilio:21211") : new(true, "SM" + Guid.NewGuid().ToString("N"), null);
    }
    public async Task<VoiceCallResult> CallAsync(string phoneNumber, string sayText, string language, CancellationToken ct = default, string? statusCallbackUrl = null)
    {
        await RecordAsync("voice", phoneNumber, statusCallbackUrl, ct, sayText);
        return File.Exists(Path.Combine(output, "fail-primary")) && phoneNumber.EndsWith("0101", StringComparison.Ordinal)
            ? new(false, null, "twilio:21211") : new(true, "CA" + Guid.NewGuid().ToString("N"), null);
    }
    public async Task SendEmailAsync(EmailMessageDto message, CancellationToken cancellationToken = default)
    { foreach (var to in message.To) await RecordAsync("email", to, null, cancellationToken, message.Body); }
    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
        => RecordAsync("email", to, null, cancellationToken, body);
}

// Límite externo sintético: se ejecutan el enriquecedor, las plantillas y el outbox reales.
// Ninguna coordenada ni mensaje del paciente sale de este host de pruebas.
sealed class SyntheticPlaces(string output) : AmazonGeoPlacesClient(new BasicAWSCredentials("e2e-only", "e2e-only"), Amazon.RegionEndpoint.USEast2)
{
    public override async Task<ReverseGeocodeResponse> ReverseGeocodeAsync(ReverseGeocodeRequest request, CancellationToken ct = default)
    {
        await File.AppendAllTextAsync(Path.Combine(output, "geocoding-ledger.jsonl"),
            JsonSerializer.Serialize(new { position = request.QueryPosition, intendedUse = request.IntendedUse.Value, request.MaxResults, request.Language }) + "\n", ct);
        var latitude = request.QueryPosition[1];
        if (Math.Abs(latitude - 11.01) < .001) await Task.Delay(TimeSpan.FromSeconds(10), ct);
        if (Math.Abs(latitude - 11.04) < .001) return new ReverseGeocodeResponse { ResultItems = [] };
        var us = latitude > 30;
        return new ReverseGeocodeResponse
        {
            ResultItems = [new ReverseGeocodeResultItem
            {
                Distance = 12,
                Address = new Address
                {
                    Street = us ? "Pennsylvania Avenue NW" : Math.Abs(latitude - 11.02) < .001 ? "<script>alert(1)</script>" : "Calle 72",
                    AddressNumber = us ? "1600" : "45-12",
                    Locality = us ? "Washington" : "Barranquilla",
                    Region = new Region { Name = us ? "District of Columbia" : "Atlántico" },
                    Country = new Country { Name = us ? "Estados Unidos" : "Colombia" },
                },
            }],
        };
    }
}
