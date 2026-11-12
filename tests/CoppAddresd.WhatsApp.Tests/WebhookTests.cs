using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CoppAddresd.WhatsApp.Persistence;
using CoppAddresd.WhatsApp.Webhook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Integration")]
public sealed class WebhookTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    public static readonly WhatsAppOptions Options = new()
    { Enabled = true, AppSecret = "test-secret", WebhookVerifyToken = "test-verify", PhoneNumberId = "test-phone", AppId = "test-app", AccessToken = "test-token" };

    private async Task<IHost> StartHost(bool enabled = true) => await new HostBuilder().ConfigureWebHost(web => web
        .UseTestServer().ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddControllers().AddApplicationPart(typeof(WhatsAppWebhookController).Assembly);
            if (enabled)
            {
                services.AddSingleton(Options);
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<WhatsAppQueue>();
                services.AddScoped<WhatsAppWebhook>();
                services.AddDbContext<WhatsAppDbContext>(db => db.UseNpgsql(fixture.ConnectionString));
            }
        }).Configure(app => app.UseRouting().UseEndpoints(endpoints => endpoints.MapControllers())))
        .StartAsync(CancellationToken.None);

    [Fact]
    public async Task Handshake_ValidInvalidModeAndDisabled()
    {
        using var host = await StartHost();
        var client = host.GetTestClient();
        var response = await client.GetAsync("/api/v1/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=test-verify&hub.challenge=abc123");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("abc123", await response.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=bad&hub.challenge=abc123")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/whatsapp/webhook?hub.mode=other&hub.verify_token=test-verify&hub.challenge=abc123")).StatusCode);
        using var disabled = await StartHost(false);
        Assert.Equal(HttpStatusCode.NotFound, (await disabled.GetTestClient().GetAsync("/api/v1/whatsapp/webhook")).StatusCode);
    }

    [Fact]
    public async Task Signatures_DedupeConcurrency_AndFastAcknowledgement()
    {
        using var host = await StartHost();
        var client = host.GetTestClient();
        var wamid = "wamid." + Guid.NewGuid();
        var body = Payload(wamid);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/whatsapp/webhook", new StringContent(body))).StatusCode);
        using var invalid = Request(body, "sha256=" + new string('0', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalid)).StatusCode);
        await using var db = fixture.CreateDb();
        Assert.False(await db.RawEvents.AnyAsync(x => x.Wamid == wamid));
        var timer = Stopwatch.StartNew();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.SendAsync(Request(body))));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), timer.Elapsed.ToString());
        var row = await db.RawEvents.SingleAsync(x => x.Wamid == wamid);
        Assert.Equal(1, row.DuplicateCount);
        Assert.Null(row.ProcessedAt);
        using var actual = System.Text.Json.JsonDocument.Parse(row.Payload);
        Assert.Equal(wamid, actual.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value").GetProperty("messages")[0].GetProperty("id").GetString());
    }

    public static HttpRequestMessage Request(string body, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/whatsapp/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Hub-Signature-256", signature ?? "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Options.AppSecret), Encoding.UTF8.GetBytes(body))));
        return request;
    }

    public static string Payload(string wamid, string text = "hola", string from = "573053924819") =>
        System.Text.Json.JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = "test-phone" }, messages = new[] { new { id = wamid, from, timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), type = "text", text = new { body = text } } } } } } } } });
}
