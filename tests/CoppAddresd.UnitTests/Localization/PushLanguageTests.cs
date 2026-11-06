using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.Extensions.Configuration;
using CoppAddresd.Application.Features.Notifications;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

public sealed class PushLanguageTests
{
    private static DeviceToken Device(string language, string token = "token") => new()
        { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Token = token, Platform = "ios", Language = language };

    [Theory]
    [InlineData(" EN ", "en")]
    [InlineData("ES", "es")]
    [InlineData(null, "en")]
    public async Task Upsert_normalizes_language_and_omission_preserves_existing(string? input, string expected)
    {
        var repo = Substitute.For<IDeviceTokenRepository>();
        var existing = Device("en");
        repo.RegisterForUserAsync(existing.UserId, "token", "ios", Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            existing.Platform = call.ArgAt<string>(2);
            existing.Language = call.ArgAt<string?>(3) ?? existing.Language;
            return existing;
        });
        var handler = new RegisterDeviceTokenCommandHandler(repo, NullLogger<RegisterDeviceTokenCommandHandler>.Instance);
        var dto = await handler.Handle(new(existing.UserId, " token ", "IOS", input), CancellationToken.None);
        Assert.Equal(expected, dto.Language);
        Assert.Equal("ios", existing.Platform);
        await repo.Received(1).RegisterForUserAsync(existing.UserId, "token", "ios", input?.Trim().ToLowerInvariant(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_legacy_device_defaults_to_spanish()
    {
        var repo = Substitute.For<IDeviceTokenRepository>();
        repo.RegisterForUserAsync(Arg.Any<Guid>(), "token", "ios", null, Arg.Any<CancellationToken>()).Returns(call =>
            Device("es"));
        var handler = new RegisterDeviceTokenCommandHandler(repo, NullLogger<RegisterDeviceTokenCommandHandler>.Instance);
        var dto = await handler.Handle(new(Guid.NewGuid(), "token", "ios"), CancellationToken.None);
        Assert.Equal("es", dto.Language);
        await repo.Received(1).RegisterForUserAsync(Arg.Any<Guid>(), "token", "ios", null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("en-US")]
    public async Task Invalid_language_is_rejected_before_repository_access(string language)
    {
        var repo = Substitute.For<IDeviceTokenRepository>();
        var handler = new RegisterDeviceTokenCommandHandler(repo, NullLogger<RegisterDeviceTokenCommandHandler>.Instance);
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => handler.Handle(new(Guid.NewGuid(), "token", "ios", language), CancellationToken.None));
        Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData("🚨 Alerta SOS activa", "Un paciente activó su alerta SOS de emergencia. Revísala de inmediato en la plataforma.", "🚨 Active SOS alert", "A patient activated an emergency SOS alert. Review it immediately in the platform.")]
    [InlineData("⭐ ¡Nivel 12!", "⭐ ¡Subiste a Nivel 12!", "⭐ Level 12!", "⭐ You reached level 12!")]
    [InlineData("🏆 ¡22 días!", "🏆 ¡22 días! +120 XP · ¡x2 por 48h!", "🏆 22 days!", "🏆 22 days! +120 XP · x2 for 48h!")]
    [InlineData("✅ Día perfecto", "✅ Día perfecto · +25 XP", "✅ Perfect day", "✅ Perfect day · +25 XP")]
    [InlineData("💊 ¡Nutriobiótico!", "💊 ¡21 días tomando tu Nutriobiótico!", "💊 Nutriobiotic!", "💊 21 days taking your Nutriobiotic!")]
    public async Task Reviewed_critical_and_gamification_copy_never_calls_provider(string title, string body, string titleEn, string bodyEn)
    {
        var provider = Substitute.For<IContentTranslationService>();
        var result = await PushContentLocalization.PrepareAsync([Device("en"), Device("es")], title, body, provider, CancellationToken.None);
        Assert.Equal(new PushContent(titleEn, bodyEn), result["en"]);
        Assert.Equal(new PushContent(title, body), result["es"]);
        Assert.Empty(provider.ReceivedCalls());
    }

    [Fact]
    public async Task Every_shipped_control_template_and_followup_has_reviewed_English_without_provider()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/CoppAddresd.Api/appsettings.Example.json"));
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(path), new JsonDocumentOptions
        { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var template in config.RootElement.GetProperty("Program").GetProperty("Controls").GetProperty("Templates").EnumerateArray())
        {
            var title = template.GetProperty("Title").GetString()!; var body = template.GetProperty("Message").GetString()!;
            var result = await PushContentLocalization.PrepareAsync([Device("en")], title, body, null, CancellationToken.None);
            Assert.NotEqual(title, result["en"].Title); Assert.NotEqual(body, result["en"].Body);
        }
        var followup = new ProgramControlSettings().FollowupTemplate.Replace("{day}", "21");
        var next = await PushContentLocalization.PrepareAsync([Device("en")], "Control del día 21", followup, null, CancellationToken.None);
        Assert.Equal("Day 21 check-in", next["en"].Title);
        Assert.StartsWith("Your day 21 check-in is still pending:", next["en"].Body);
    }

    [Fact]
    public async Task Gamification_projects_per_device_without_changing_priority_or_canonical_log()
    {
        var log = Substitute.For<INotificationLogRepository>(); var tokens = Substitute.For<IDeviceTokenRepository>();
        var fcm = Substitute.For<IFcmClient>(); var user = Guid.NewGuid(); var patient = Guid.NewGuid();
        log.GetContextAsync(patient, Arg.Any<CancellationToken>()).Returns(new NotificationContext(user, "UTC"));
        tokens.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(new[] { Device("es", "es"), Device("en", "en") });
        fcm.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>()).Returns(new FcmSendResult(FcmSendStatus.Sent));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Program:Notifications:QuietHoursStart"] = "0", ["Program:Notifications:QuietHoursEnd"] = "0" }).Build();
        var service = new GamifiedNotificationService(log, tokens, fcm, configuration, NullLogger<GamifiedNotificationService>.Instance);
        await service.NotifyAsync(patient, "level_up", "⭐ ¡Nivel 12!", "⭐ ¡Subiste a Nivel 12!", "high");
        await fcm.Received(1).SendAsync("en", "⭐ Level 12!", "⭐ You reached level 12!", Arg.Is<Dictionary<string,string>?>(d => d!["type"] == "level_up" && d["patient_id"] == patient.ToString()), Arg.Any<CancellationToken>());
        await fcm.Received(1).SendAsync("es", "⭐ ¡Nivel 12!", "⭐ ¡Subiste a Nivel 12!", Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>());
        await log.Received(1).AddAsync(Arg.Is<AppNotification>(n => n.Title == "⭐ ¡Nivel 12!" && n.Message == "⭐ ¡Subiste a Nivel 12!" && n.Priority == "high"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generic_batch_localizes_before_fanout_keeps_routing_and_injects_canonical_chat_once()
    {
        var repo = Substitute.For<IDeviceTokenRepository>(); var fcm = Substitute.For<IFcmClient>();
        var ai = Substitute.For<IAiServiceClient>(); var provider = Substitute.For<IContentTranslationService>();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { Device("es", "es-token"), Device("en", "en-token"), Device("en", "second-en") });
        var translated = false;
        provider.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.Equal(new[] { "Título propio", "Texto propio 5 mg" }, call.Arg<IReadOnlyCollection<string>>());
            Assert.Empty(fcm.ReceivedCalls()); translated = true;
            return new Dictionary<string, string> { ["Título propio"] = "Custom title", ["Texto propio 5 mg"] = "Custom text 5 mg" };
        });
        fcm.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { Assert.True(translated); return new FcmSendResult(FcmSendStatus.Sent); });
        var handler = new SendPushNotificationCommandHandler(repo, fcm, ai, NullLogger<SendPushNotificationCommandHandler>.Instance, provider);
        var result = await handler.Handle(new(userId, "Título propio", "Texto propio 5 mg", Data: new Dictionary<string,string> { ["route"] = "/program" }), CancellationToken.None);
        Assert.Equal(3, result.SentCount);
        await fcm.Received(1).SendAsync("es-token", "Título propio", "Texto propio 5 mg", Arg.Is<Dictionary<string,string>?>(d => d!["route"] == "/program"), Arg.Any<CancellationToken>());
        await fcm.Received(1).SendAsync("en-token", "Custom title", "Custom text 5 mg", Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>());
        await provider.Received(1).TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>());
        await ai.Received(1).ProactiveMessageAsync(userId, "Texto propio 5 mg", "base", "bot", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_translation_aborts_before_any_token_or_chat_is_sent()
    {
        var repo = Substitute.For<IDeviceTokenRepository>(); var fcm = Substitute.For<IFcmClient>(); var ai = Substitute.For<IAiServiceClient>();
        repo.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new[] { Device("es"), Device("en") });
        var handler = new SendPushNotificationCommandHandler(repo, fcm, ai, NullLogger<SendPushNotificationCommandHandler>.Instance);
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => handler.Handle(new(Guid.NewGuid(), "Título propio", "Texto propio"), CancellationToken.None));
        Assert.Empty(fcm.ReceivedCalls()); Assert.Empty(ai.ReceivedCalls());
    }

    [Fact]
    public async Task Supplied_bilingual_copy_is_preferred_and_chat_override_is_used_once()
    {
        var repo = Substitute.For<IDeviceTokenRepository>(); var fcm = Substitute.For<IFcmClient>(); var ai = Substitute.For<IAiServiceClient>();
        var provider = Substitute.For<IContentTranslationService>(); var user = Guid.NewGuid();
        repo.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(new[] { Device("en") });
        fcm.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>()).Returns(new FcmSendResult(FcmSendStatus.Sent));
        var handler = new SendPushNotificationCommandHandler(repo, fcm, ai, NullLogger<SendPushNotificationCommandHandler>.Instance, provider);
        await handler.Handle(new(user, "Listo", "Programa listo", EnglishTitle: "Ready", EnglishBody: "Program ready", ChatBody: "Program ready"), CancellationToken.None);
        await fcm.Received(1).SendAsync("token", "Ready", "Program ready", Arg.Any<Dictionary<string,string>?>(), Arg.Any<CancellationToken>());
        await ai.Received(1).ProactiveMessageAsync(user, "Program ready", "base", "bot", Arg.Any<CancellationToken>());
        Assert.Empty(provider.ReceivedCalls());
    }
}
