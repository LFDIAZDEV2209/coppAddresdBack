using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Application.WhatsApp;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.Infrastructure.Services;
using CoppAddresd.WhatsApp.Messaging;
using CoppAddresd.WhatsApp.Persistence;
using CoppAddresd.WhatsApp.Webhook;
using CoppAddresd.WhatsApp.Reminders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Integration")]
public sealed class InboundFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task TwoEnglishTurns_UseRealChatHandlerAndHttpContract_WithStableThread_AndDedupe()
    {
        await using var harness = await CreateAsync();
        var firstId = Guid.NewGuid().ToString();
        await harness.EnqueueAsync(firstId, "Hello, how are you?");
        await harness.EnqueueAsync(firstId, "Hello, how are you?");
        Assert.True(await harness.ProcessAsync());
        Assert.False(await harness.ProcessAsync());
        await harness.EnqueueAsync(Guid.NewGuid().ToString(), "I feel better today");
        await harness.ProcessAsync();
        Assert.Equal(2, harness.Requests.Count);
        var first = harness.Requests[0];
        var second = harness.Requests[1];
        Assert.Equal(first.GetProperty("thread_id").GetString(), second.GetProperty("thread_id").GetString());
        Assert.Equal(harness.PatientId.ToString(), first.GetProperty("patient_id").GetString());
        Assert.Equal(harness.UserId.ToString(), first.GetProperty("user_id").GetString());
        Assert.Equal("base", first.GetProperty("agent").GetString());
        Assert.All(harness.Requests, request => { Assert.Equal("en", request.GetProperty("language").GetString()); Assert.False(request.TryGetProperty("control_context", out _)); });
        await using var db = fixture.CreateDb();
        Assert.Equal("en", (await db.Conversations.SingleAsync()).Language);
        Assert.Equal(2, harness.Sent.Count);
        Assert.All(await db.RawEvents.ToListAsync(), x => Assert.NotNull(x.ProcessedAt));
        Assert.Equal(1, (await db.RawEvents.SingleAsync(x => x.Wamid == firstId)).DuplicateCount);
    }

    [Theory]
    [InlineData("SÁLIR", true, "es")]
    [InlineData("sToP", true, "en")]
    [InlineData("INÍCIAR", false, "es")]
    [InlineData("sTaRt", false, "en")]
    public async Task Commands_UpdatePreference_WithoutAgent(string command, bool optOut, string language)
    {
        await using var harness = await CreateAsync();
        await harness.EnqueueAsync(Guid.NewGuid().ToString(), command);
        await harness.ProcessAsync();
        Assert.Empty(harness.Requests);
        Assert.Single(harness.Sent);
        await using var db = fixture.CreateDb();
        var link = await db.PatientLinks.SingleAsync();
        Assert.Equal(optOut, link.OptOut);
        Assert.Equal(language, link.Language);
    }

    [Fact]
    public async Task AgentFailure_SendsSafeMessage_AndRetainsCause()
    {
        await using var harness = await CreateAsync(failAgent: true);
        await harness.EnqueueAsync(Guid.NewGuid().ToString(), "Hola");
        await harness.ProcessAsync();
        Assert.Contains("no está disponible", Assert.Single(harness.Sent));
        await using var db = fixture.CreateDb();
        var input = await db.RawEvents.SingleAsync();
        Assert.StartsWith("agent_", input.ErrorCode);
        Assert.NotNull(input.ProcessedAt);
    }

    [Fact]
    public async Task PatientReply_CancelsPendingReminderBeforeAck_AndContinuesAgent()
    {
        await using var harness = await CreateAsync();
        await using var db = fixture.CreateDb();
        db.Reminders.Add(new Reminder { PatientId = harness.PatientId, SourceKey = "interaction-test",
            Kind = ReminderKind.checkin, ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(120) });
        await db.SaveChangesAsync();
        await harness.EnqueueAsync(Guid.NewGuid().ToString(), "Hola, me siento bien");
        Assert.Equal(ReminderState.Cancelled, (await db.Reminders.AsNoTracking().SingleAsync()).State);
        await harness.ProcessAsync();
        Assert.Single(harness.Requests);
    }

    [Fact]
    public async Task TransientSendFailure_ReplaysSavedReply_WithoutAnotherAgentCall()
    {
        await using var harness = await CreateAsync();
        harness.FailNextSend = true;
        await harness.EnqueueAsync(Guid.NewGuid().ToString(), "Hola");
        await harness.ProcessAsync();
        await using (var db = fixture.CreateDb())
        {
            var input = await db.RawEvents.SingleAsync();
            Assert.Null(input.ProcessedAt);
            input.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await harness.ProcessAsync();
        Assert.Single(harness.Requests);
        await using var final = fixture.CreateDb();
        Assert.NotNull((await final.RawEvents.SingleAsync()).ProcessedAt);
    }

    private async Task<Harness> CreateAsync(bool failAgent = false)
    {
        await using var db = fixture.CreateDb();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM whatsapp.reminder_deliveries; DELETE FROM whatsapp.reminders; DELETE FROM whatsapp.raw_events; DELETE FROM whatsapp.conversations; DELETE FROM whatsapp.patient_links;", CancellationToken.None);
        var harness = new Harness();
        db.PatientLinks.Add(new PatientLink { PatientId = harness.PatientId, UserId = harness.UserId,
            WaId = "+573053924819", FirstName = "Ana", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        harness.AiHost = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().Configure(app => app.Run(async context =>
        {
            Assert.Equal("/api/v1/chat", context.Request.Path.Value);
            Assert.Equal("test-internal", context.Request.Headers["X-Internal-Key"]);
            using var payload = await JsonDocument.ParseAsync(context.Request.Body);
            harness.Requests.Add(payload.RootElement.Clone());
            if (failAgent) { context.Response.StatusCode = 503; await context.Response.WriteAsync("{\"detail\":\"offline\"}"); return; }
            await context.Response.WriteAsJsonAsync(new { answer = "Respuesta del agente", thread_id = payload.RootElement.GetProperty("thread_id").GetString(), execution_id = "test" });
        }))).StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ChatCommand>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(WebhookTests.Options);
        services.AddSingleton<WhatsAppQueue>();
        services.AddSingleton(Substitute.For<IAgentCatalogRepository>());
        services.AddSingleton(Substitute.For<IAgentRuntimeSyncService>());
        services.AddSingleton(Substitute.For<IProgramControlRepository>());
        services.AddSingleton(Substitute.For<IPatientChannelQuery>());
        services.AddSingleton<IOptions<ProgramControlSettings>>(Options.Create(new ProgramControlSettings()));
        services.AddSingleton<IOptions<AiServiceSettings>>(Options.Create(new AiServiceSettings { BaseUrl = "http://localhost", InternalApiKey = "test-internal" }));
        services.AddScoped<IAiServiceClient>(sp => new AiServiceClient(harness.AiHost.GetTestClient(), sp.GetRequiredService<IOptions<AiServiceSettings>>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AiServiceClient>>()));
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (harness.FailNextSend) { harness.FailNextSend = false; return WhatsAppSendResult.Failed("429", WhatsAppFailureKind.Transient); }
            harness.Sent.Add(call.ArgAt<string>(1));
            return new WhatsAppSendResult("wamid.reply");
        });
        services.AddSingleton(sender);
        services.AddDbContext<WhatsAppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<WhatsAppPatientResolver>();
        services.AddScoped<WhatsAppWebhook>();
        services.AddScoped<WhatsAppInboundProcessor>();
        services.AddSingleton(new ReminderSettings());
        services.AddSingleton(Substitute.For<IProgramAccompanimentQuery>());
        services.AddScoped<WhatsAppReminderScheduler>();
        services.AddScoped<IWhatsAppReminderHooks, WhatsAppReminderHooks>();
        harness.Services = services.BuildServiceProvider();
        return harness;
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Guid PatientId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public IHost AiHost { get; set; } = null!;
        public ServiceProvider Services { get; set; } = null!;
        public List<JsonElement> Requests { get; } = [];
        public List<string> Sent { get; } = [];
        public bool FailNextSend { get; set; }
        public async Task EnqueueAsync(string wamid, string text)
        {
            using var scope = Services.CreateScope();
            var body = Encoding.UTF8.GetBytes(WebhookTests.Payload(wamid, text));
            var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookTests.Options.AppSecret), body));
            Assert.Equal(200, await scope.ServiceProvider.GetRequiredService<WhatsAppWebhook>().AcceptAsync(body, signature, CancellationToken.None));
        }
        public async Task<bool> ProcessAsync()
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WhatsAppInboundProcessor>().ProcessNextAsync(CancellationToken.None);
        }
        public async ValueTask DisposeAsync() { await Services.DisposeAsync(); AiHost.Dispose(); }
    }
}

[Trait("Category", "Unit")]
public sealed class LanguageTests
{
    [Theory]
    [InlineData("Hello, how are you?", "es", "en")]
    [InlineData("I feel better", "en", "en")]
    [InlineData("Hola, me siento mejor", "en", "es")]
    [InlineData("ok", "en", "en")]
    [InlineData("ok", "es", "es")]
    public void Language_ChangesOnlyWithClearSignal(string text, string previous, string expected) => Assert.Equal(expected, TurnLanguage.Detect(text, previous));
}
