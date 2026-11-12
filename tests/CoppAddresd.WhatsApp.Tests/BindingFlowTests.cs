using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.WhatsApp.Binding;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.WhatsApp.Binding;
using CoppAddresd.WhatsApp.Messaging;
using CoppAddresd.WhatsApp.Persistence;
using CoppAddresd.WhatsApp.Webhook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Integration")]
public sealed class BindingFlowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly string document = "9" + RandomNumberGenerator.GetInt32(10000000, 99999999);
    private readonly Guid patientId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly ManualClock clock = new() { Now = DateTimeOffset.UtcNow };
    private readonly ConcurrentQueue<string> calls = new();
    private readonly List<string> sent = [];
    private readonly CapturedLogs logs = new();
    private readonly IAiServiceClient ai = Substitute.For<IAiServiceClient>();
    private ServiceProvider services = null!;
    private IHost authHost = null!;
    private int sendStatus = 200;
    private int verifyStatus = 200;
    private bool wrongSubject;
    private bool missingPhoneProof;
    private bool expireLeaseDuringVerification;

    public async Task InitializeAsync()
    {
        await fixture.CreateAppSchemaAsync(CancellationToken.None);
        await using var db = fixture.CreateDb();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM whatsapp.binding_sessions; DELETE FROM whatsapp.reminder_deliveries; DELETE FROM whatsapp.reminders; DELETE FROM whatsapp.raw_events; DELETE FROM whatsapp.conversations; DELETE FROM whatsapp.patient_links;", CancellationToken.None);
        await using var app = fixture.CreateAppDb();
        app.PatientProfiles.Add(new PatientProfile { Id = patientId, UserId = userId, FirstName = "Paciente", LastName = "QA",
            DocumentNumber = document, PhoneCountryCode = "57", PhoneNumber = "3001234567" });
        await app.SaveChangesAsync();
        authHost = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().Configure(server => server.Run(async context =>
        {
            Assert.Equal("POST", context.Request.Method);
            Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-For"));
            Assert.False(context.Request.Headers.ContainsKey("Cookie"));
            using var body = await JsonDocument.ParseAsync(context.Request.Body);
            Assert.Equal(document, body.RootElement.GetProperty("documentNumber").GetString());
            Assert.False(body.RootElement.TryGetProperty("password", out _));
            var route = context.Request.Path.Value!;
            calls.Enqueue(route);
            switch (route)
            {
                case "/api/auth/id-lookup":
                    Assert.Equal("app", body.RootElement.GetProperty("application").GetString());
                    await context.Response.WriteAsJsonAsync(new { patientId, firstName = "Paciente", lastName = "QA", documentNumber = document,
                        contacts = new[] { new { id = "phone", type = "Phone", label = "+57 30•••67" } } });
                    break;
                case "/api/auth/send-otp":
                    Assert.Equal("phone", body.RootElement.GetProperty("contactId").GetString());
                    context.Response.StatusCode = sendStatus;
                    await context.Response.WriteAsJsonAsync(new { expiresIn = 300, devCode = "never-forward-this" });
                    break;
                case "/api/auth/verify-otp":
                    if (expireLeaseDuringVerification) clock.Now = clock.Now.AddMinutes(3);
                    Assert.Equal("phone", body.RootElement.GetProperty("contactId").GetString());
                    Assert.Equal("app", body.RootElement.GetProperty("application").GetString());
                    Assert.Equal("654321", body.RootElement.GetProperty("otp").GetString());
                    context.Response.StatusCode = verifyStatus;
                    // Contrato LoginResponse real; token ficticio solo del servidor simulado.
                    var subject = wrongSubject ? Guid.NewGuid() : userId;
                    var payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { sub = subject.ToString(), aud = "app" })).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    context.Response.Headers.Append("Set-Cookie", "copp_refresh_token=not-a-real-token; HttpOnly");
                    await context.Response.WriteAsJsonAsync(new { accessToken = "eyJhbGciOiJub25lIn0." + payload + ".test", tokenType = "Bearer", expiresIn = 900,
                        verifiedContactId = missingPhoneProof ? null : "phone" });
                    break;
                default: context.Response.StatusCode = 501; break;
            }
        }))).StartAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString, ["WhatsApp:Auth:BaseUrl"] = "http://auth.test" }).Build();
        var collection = new ServiceCollection();
        collection.AddLogging(builder => builder.AddProvider(logs));
        collection.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ChatCommand>());
        collection.AddWhatsApp(config, options: WebhookTests.Options);
        collection.AddSingleton<TimeProvider>(clock);
        collection.AddDbContext<CoppAddresd.Infrastructure.Persistence.AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        collection.AddScoped<IPatientBindingQuery, PatientBindingQuery>();
        collection.AddScoped<IPatientChannelQuery, PatientChannelQuery>();
        collection.AddSingleton(Substitute.For<IProgramAccompanimentQuery>());
        collection.AddSingleton(Substitute.For<IAgentCatalogRepository>());
        collection.AddSingleton(Substitute.For<IAgentRuntimeSyncService>());
        collection.AddSingleton(Substitute.For<IProgramControlRepository>());
        collection.AddSingleton<IOptions<ProgramControlSettings>>(Options.Create(new ProgramControlSettings()));
        ai.ChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>()).Returns(c => new ChatResponse("Respuesta del agente", c.Arg<ChatRequest>().ThreadId!));
        collection.AddSingleton(ai);
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(c =>
        { sent.Add(c.ArgAt<string>(1)); return new WhatsAppSendResult("wamid.reply"); });
        collection.AddSingleton(sender);
        collection.AddHttpClient<IWhatsAppOtpClient, AuthOtpClient>().ConfigurePrimaryHttpMessageHandler(() => authHost.GetTestServer().CreateHandler());
        services = collection.BuildServiceProvider();
    }

    public async Task DisposeAsync() { if (services is not null) await services.DisposeAsync(); authHost?.Dispose(); }

    [Fact]
    public async Task DocumentThenApprovedCode_LinksOnlyAfterVerification_AndNextMessageCallsAgent()
    {
        await Turn("hola");
        Assert.Contains("VINCULAR", sent[^1]);
        await Turn("VINCULAR");
        await Turn(document);
        Assert.Contains("573***567", sent[^1]);
        Assert.DoesNotContain("3001234567", sent[^1]);
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
        Assert.Empty(await db.Conversations.ToListAsync());
        await Turn("654321");
        Assert.Equal(patientId, (await db.PatientLinks.SingleAsync()).PatientId);
        Assert.Contains("Número vinculado", sent[^1]);
        Assert.Equal(new[] { "/api/auth/id-lookup", "/api/auth/send-otp", "/api/auth/verify-otp" }, calls.ToArray());
        await ai.DidNotReceive().ChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
        await Turn("Hola, ¿cómo estás?");
        await ai.Received(1).ChatAsync(Arg.Is<ChatRequest>(r => r.PatientId == patientId.ToString()), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(logs.Messages, x => x.Contains(document) || x.Contains("654321") || x.Contains("not-a-real-token"));
        Assert.DoesNotContain(sent, x => x.Contains("never-forward-this") || x.Contains("not-a-real-token") || x.Contains("eyJhbGci"));
        var privateEvents = await db.RawEvents.Where(x => x.Reply != "Respuesta del agente").ToListAsync();
        Assert.All(privateEvents, input => { Assert.Equal("{}", input.Payload); Assert.Equal("[vinculación]", input.Text); });
    }

    [Theory]
    [InlineData("missing")] [InlineData("inactive")] [InlineData("ambiguous")] [InlineData("deleted")]
    public async Task MissingInactiveDeletedOrAmbiguousDocument_HasSameGenericReplyAndNoAuth(string condition)
    {
        await using var app = fixture.CreateAppDb();
        if (condition == "missing") await app.PatientProfiles.Where(p => p.Id == patientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DocumentNumber, "OTHER"));
        if (condition == "inactive") await app.PatientProfiles.Where(p => p.Id == patientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Inactivo"));
        if (condition == "deleted") await app.PatientProfiles.Where(p => p.Id == patientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, DateTime.UtcNow));
        if (condition == "ambiguous")
        {
            app.PatientProfiles.Add(new PatientProfile { Id = Guid.NewGuid(), FirstName = "Otro", LastName = "QA", DocumentNumber = document });
            await app.SaveChangesAsync();
        }
        await Turn(document);
        Assert.Equal(WhatsAppBindingFlow.Guide, sent[^1]);
        Assert.Empty(calls);
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
        await ai.DidNotReceive().ChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThreeBadCodes_EndFlow_AndFourthDoesNotCallAuth()
    {
        verifyStatus = 401;
        await Turn(document);
        for (var i = 0; i < 3; i++) await Turn("654321");
        Assert.Contains("3 intentos", sent[^1]);
        await Turn("654321");
        Assert.Equal(3, calls.Count(x => x.EndsWith("verify-otp")));
        await using var db = fixture.CreateDb();
        var session = await db.BindingSessions.SingleAsync();
        Assert.Equal(3, session.Attempts);
        Assert.Equal(BindingState.Ended, session.State);
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task Resend_DoesNotResetCodeAttempts()
    {
        verifyStatus = 401;
        await Turn(document);
        await Turn("654321");
        await Turn("654321");
        await Turn("REENVIAR");
        await Turn("654321");
        await using var db = fixture.CreateDb();
        var session = await db.BindingSessions.SingleAsync();
        Assert.Equal(3, session.Attempts);
        Assert.Equal(2, session.OtpSends);
        Assert.Equal(BindingState.Ended, session.State);
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task PatientDeactivatedWhileWaiting_CannotVerifyOrLink()
    {
        await Turn(document);
        await using var app = fixture.CreateAppDb();
        await app.PatientProfiles.Where(p => p.Id == patientId).ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, "Inactivo"));
        await Turn("654321");
        Assert.DoesNotContain(calls, x => x.EndsWith("verify-otp"));
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task TenMinutes_ExpiresWithoutAuthVerification()
    {
        await Turn(document);
        clock.Now = clock.Now.AddMinutes(10);
        await Turn("654321");
        Assert.Contains("expiró", sent[^1]);
        Assert.DoesNotContain(calls, x => x.EndsWith("verify-otp"));
        await using var db = fixture.CreateDb();
        Assert.Equal(BindingState.Expired, (await db.BindingSessions.SingleAsync()).State);
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task TwoSendsPerRollingHour_SurviveCancelAndNewScope()
    {
        await Turn(document);
        await Turn("REENVIAR");
        await Turn("CANCELAR");
        await Turn("VINCULAR");
        Assert.Contains("2 envíos", sent[^1]);
        await Turn(document);
        Assert.Equal(2, calls.Count(x => x.EndsWith("send-otp")));
        clock.Now = clock.Now.AddHours(1);
        await Turn("VINCULAR");
        await Turn(document);
        Assert.Equal(3, calls.Count(x => x.EndsWith("send-otp")));
    }

    [Theory]
    [InlineData("send")] [InlineData("verify")]
    public async Task AuthRateLimit_IsRespectedWithoutAutomaticRetry(string endpoint)
    {
        if (endpoint == "send") sendStatus = 429; else verifyStatus = 429;
        await Turn(document);
        if (endpoint == "verify") await Turn("654321");
        Assert.Contains("limitó", sent[^1]);
        Assert.Single(calls.Where(x => x.EndsWith(endpoint == "send" ? "send-otp" : "verify-otp")));
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task ApprovedWrongSubject_IsNotLinked()
    {
        wrongSubject = true;
        await Turn(document);
        await Turn("654321");
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task ExpiredWorkerLease_CannotCreateBindingEvenWithApprovedCode()
    {
        expireLeaseDuringVerification = true;
        await Turn(document);
        await Turn("654321");
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task OlderAuthWithoutPhoneProof_FailsClosed()
    {
        missingPhoneProof = true;
        await Turn(document);
        await Turn("654321");
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.PatientLinks.ToListAsync());
    }

    [Fact]
    public async Task PasswordText_IsRejectedAndNotSentToAuth()
    {
        await Turn("mi contraseña es abc");
        Assert.Contains("No envíes contraseñas", sent[^1]);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task DuplicateWebhook_SendsOneSms()
    {
        var body = WebhookTests.Payload("wamid.duplicate", document);
        await Enqueue(body);
        await Enqueue(body);
        await Process();
        Assert.False(await Process());
        Assert.Single(calls.Where(x => x.EndsWith("send-otp")));
    }

    private async Task Turn(string text)
    {
        await Enqueue(WebhookTests.Payload(Guid.NewGuid().ToString(), text));
        Assert.True(await Process());
    }
    private async Task Enqueue(string payload)
    {
        using var scope = services.CreateScope();
        var body = Encoding.UTF8.GetBytes(payload);
        var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookTests.Options.AppSecret), body));
        Assert.Equal(200, await scope.ServiceProvider.GetRequiredService<WhatsAppWebhook>().AcceptAsync(body, signature, CancellationToken.None));
    }
    private async Task<bool> Process()
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WhatsAppInboundProcessor>().ProcessNextAsync(CancellationToken.None);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Collector(Messages);
        public void Dispose() { }
        private sealed class Collector(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
