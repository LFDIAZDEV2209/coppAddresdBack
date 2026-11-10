using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Api.Localization;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

/// <summary>Los lotes de traducción usan el destinatario real y nunca habilitan un proveedor por un operador.</summary>
public sealed class TranslationConsentTests
{
    private sealed class EchoTransport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var text = await request.Content!.ReadAsStringAsync(ct);
            Bodies.Add(text);
            var payload = JsonDocument.Parse(text).RootElement;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                target_language = "en",
                items = payload.GetProperty("items").EnumerateArray().Select(item => new
                { id = item.GetProperty("id").GetString(), translation = item.GetProperty("text").GetString() }).ToArray()
            }) };
        }
    }

    private static ContentTranslationService Service(IServiceScopeFactory scopes, IHttpContextAccessor http, EchoTransport transport)
        => new(new HttpClient(new AiConsentHttpHandler(scopes, http) { InnerHandler = transport }),
            Options.Create(new AiServiceSettings { BaseUrl = "https://translation.invalid", InternalApiKey = "synthetic-only" }));

    [Theory]
    [InlineData("app")]
    [InlineData("erp")]
    [InlineData(null)]
    public async Task Background_push_checks_actual_recipient_without_using_actor_or_sending_identity(string? actorAudience)
    {
        var recipient = Guid.NewGuid(); var actor = Guid.NewGuid();
        var consents = Substitute.For<ILegalConsentService>();
        using var services = new ServiceCollection().AddSingleton(consents).BuildServiceProvider();
        var http = new HttpContextAccessor();
        if (actorAudience is not null) http.HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, actor.ToString()), new("aud", actorAudience)], "test")) };
        var transport = new EchoTransport();
        var translator = Service(services.GetRequiredService<IServiceScopeFactory>(), http, transport);
        var tokens = new[] { new DeviceToken { UserId = recipient, Language = "en" }, new DeviceToken { UserId = recipient, Language = "en" } };
        var copy = await PushContentLocalization.PrepareAsync(tokens, "Título propio", "Indicación propia 5 mg", translator, default);
        Assert.Equal("Indicación propia 5 mg", copy["en"].Body);
        Assert.Equal(1, transport.Calls);
        await consents.Received(1).EnsureUserAsync(recipient, "patient-ai", Arg.Any<CancellationToken>());
        await consents.DidNotReceive().EnsureUserAsync(actor, Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(recipient.ToString(), Assert.Single(transport.Bodies));
        Assert.DoesNotContain(actor.ToString(), Assert.Single(transport.Bodies));
        http.HttpContext = null;
    }

    [Theory]
    [InlineData("CONSENT_REQUIRED", 403)]
    [InlineData("CONSENT_DOCUMENT_UNAVAILABLE", 503)]
    public async Task Unaccepted_recipient_stops_translation_before_transport_even_for_erp_actor(string code, int status)
    {
        var recipient = Guid.NewGuid();
        var consents = Substitute.For<ILegalConsentService>();
        consents.EnsureUserAsync(recipient, "patient-ai", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AiConsentException(code, "patient-ai", status)));
        using var services = new ServiceCollection().AddSingleton(consents).BuildServiceProvider();
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new("aud", "erp")], "test")) } };
        var transport = new EchoTransport();
        var translator = Service(services.GetRequiredService<IServiceScopeFactory>(), http, transport);
        var error = await Assert.ThrowsAsync<AiConsentException>(() => PushContentLocalization.PrepareAsync(
            [new DeviceToken { UserId = recipient, Language = "en" }], "Título propio", "Indicación propia", translator, default));
        Assert.Equal(code, error.Code);
        Assert.Equal(0, transport.Calls);
        await consents.Received(1).EnsureUserAsync(recipient, "patient-ai", Arg.Any<CancellationToken>());
        http.HttpContext = null;
    }

    [Fact]
    public async Task A_later_batch_rechecks_recipient_and_stops_after_withdrawal()
    {
        var user = Guid.NewGuid(); var checks = 0;
        var consents = Substitute.For<ILegalConsentService>();
        consents.EnsureUserAsync(user, "patient-ai", Arg.Any<CancellationToken>()).Returns(_ =>
        { checks++; return checks == 1 ? Task.CompletedTask : Task.FromException(new AiConsentException("CONSENT_REQUIRED", "patient-ai", 403)); });
        using var services = new ServiceCollection().AddSingleton(consents).BuildServiceProvider();
        var transport = new EchoTransport();
        var translator = Service(services.GetRequiredService<IServiceScopeFactory>(), new HttpContextAccessor(), transport);
        await Assert.ThrowsAsync<AiConsentException>(() => translator.TranslateAsync(
            Enumerable.Range(1, 65).Select(index => $"Texto {index}").ToArray(), "en", default,
            new ContentTranslationSubject("patient-ai", user)));
        Assert.Equal(2, checks);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Mixed_or_missing_recipients_are_rejected_before_translation()
    {
        var translations = Substitute.For<IContentTranslationService>();
        foreach (var devices in new[]
        {
            new[] { new DeviceToken { UserId = Guid.Empty, Language = "en" } },
            new[] { new DeviceToken { UserId = Guid.NewGuid(), Language = "en" }, new DeviceToken { UserId = Guid.NewGuid(), Language = "en" } }
        })
        {
            var error = await Assert.ThrowsAsync<AiConsentException>(() => PushContentLocalization.PrepareAsync(
                devices, "Título propio", "Indicación propia", translations, default));
            Assert.Equal("CONSENT_SUBJECT_REQUIRED", error.Code);
        }
        Assert.Empty(translations.ReceivedCalls());
    }

    [Theory]
    [InlineData("CONSENT_REQUIRED", 403)]
    [InlineData("CONSENT_DOCUMENT_UNAVAILABLE", 503)]
    [InlineData("CONSENT_SUBJECT_REQUIRED", 503)]
    public async Task Read_only_projection_preserves_original_authored_content_when_consent_is_missing(string code, int status)
    {
        var translations = Substitute.For<IContentTranslationService>();
        translations.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<string, string>>(new AiConsentException(code, "patient-ai", status)));
        var filter = new PatientContentLocalizationFilter(new(translations), Options.Create(new JsonOptions()));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("aud", "app")], "test")) };
        http.Request.Headers["X-App-Language"] = "en";
        var descriptor = new ActionDescriptor { EndpointMetadata = [new LocalizePatientContentAttribute(PatientContentProfile.NutritionPlan)] };
        var original = new { name = "Plan revisado", quantity = 150 };
        var result = new OkObjectResult(original);
        var context = new ResultExecutingContext(new ActionContext(http, new(), descriptor), [], result, new object());
        var called = false;
        await filter.OnResultExecutionAsync(context, () => { called = true; return Task.FromResult(new ResultExecutedContext(context, [], result, context.Controller)); });
        Assert.True(called);
        Assert.Same(original, result.Value);
        Assert.Equal("es", http.Response.Headers.ContentLanguage);
        Assert.Contains("Authorization", http.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task Read_only_projection_does_not_hide_unrelated_consent_errors()
    {
        var translations = Substitute.For<IContentTranslationService>();
        translations.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<string, string>>(new AiConsentException("UNEXPECTED", "patient-ai", 500)));
        var filter = new PatientContentLocalizationFilter(new(translations), Options.Create(new JsonOptions()));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("aud", "app")], "test")) };
        http.Request.Headers["X-App-Language"] = "en";
        var descriptor = new ActionDescriptor { EndpointMetadata = [new LocalizePatientContentAttribute(PatientContentProfile.NutritionPlan)] };
        var result = new OkObjectResult(new { name = "Plan revisado" });
        var context = new ResultExecutingContext(new ActionContext(http, new(), descriptor), [], result, new object());
        await Assert.ThrowsAsync<AiConsentException>(() => filter.OnResultExecutionAsync(context,
            () => throw new InvalidOperationException("No success expected")));
    }
}
