using System.Net;
using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.LegalDocuments;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests;

public sealed class LegalConsentTests : IDisposable
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"consent-{Guid.NewGuid()}").Options);
    private readonly LegalConsentSettings configuration = new();
    private readonly IOptionsMonitor<LegalConsentSettings> monitor = Substitute.For<IOptionsMonitor<LegalConsentSettings>>();
    private readonly Guid user = Guid.NewGuid();
    private static readonly DateTime Published = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    public LegalConsentTests() => monitor.CurrentValue.Returns(configuration);
    public void Dispose() => db.Dispose();
    private LegalConsentService Service() => new(db, monitor, TimeProvider.System);

    private async Task<LegalDocumentVersion> Configure(string locale = "es")
    {
        var doc = new LegalDocument { Id = Guid.NewGuid(), Code = $"synthetic-chat-{locale}", Title = "Synthetic disclosure" };
        var version = new LegalDocumentVersion { Id = Guid.NewGuid(), DocumentId = doc.Id, Document = doc,
            Major = 1, Minor = 0, IsPublished = true, Content = "Synthetic test content only.", CreatedAt = Published };
        doc.CurrentVersionId = version.Id;
        db.LegalDocuments.Add(doc);
        db.LegalDocumentVersions.Add(version);
        if (!configuration.Scopes.TryGetValue("chat", out var config))
            configuration.Scopes["chat"] = config = new() { Approved = true, ProcessingRevision = "synthetic-v1", Providers = ["synthetic-provider"] };
        config.Documents[locale] = doc.Code;
        await db.SaveChangesAsync();
        return version;
    }

    private async Task<AcceptLegalConsentRequest> Request(string locale = "es")
    {
        var status = (await Service().GetStatusAsync(user, locale, default)).Scopes.Single(s => s.Scope == "chat");
        return new("chat", status.Documents.Single().VersionId, locale, status.DisclosureId!, Guid.NewGuid());
    }

    [Fact]
    public async Task Missing_unapproved_or_unpublished_configuration_never_authorizes()
    {
        Assert.All((await Service().GetStatusAsync(user, "es", default)).Scopes, s => Assert.False(s.Available));
        Assert.Equal(503, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        var version = await Configure();
        configuration.Scopes["chat"].Approved = false;
        Assert.Equal(503, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        configuration.Scopes["chat"].Approved = true;
        version.IsPublished = false;
        await db.SaveChangesAsync();
        Assert.False((await Service().GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat").Available);
    }

    [Fact]
    public async Task Receipt_is_bound_to_user_published_version_locale_and_disclosure_and_retry_is_idempotent()
    {
        var version = await Configure();
        var request = await Request();
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        var receipt = await Service().AcceptAsync(user, request, default);
        Assert.Equal(version.Id, receipt.VersionId);
        Assert.Equal("es", receipt.Locale);
        Assert.Equal(receipt, await Service().AcceptAsync(user, request, default));
        Assert.Equal(1, await db.LegalDocumentAcceptances.CountAsync());
        await Service().EnsureUserAsync(user, "chat", default);
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(Guid.NewGuid(), "chat", default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AiConsentException>(() => Service().AcceptAsync(user,
            request with { Locale = "en" }, default))).StatusCode);
    }

    [Fact]
    public async Task Draft_wrong_manifest_and_changed_active_version_cannot_be_accepted()
    {
        var version = await Configure();
        var request = await Request();
        var draft = new LegalDocumentVersion { Id = Guid.NewGuid(), DocumentId = version.DocumentId,
            Major = 1, Minor = 1, Content = "Synthetic draft", IsPublished = false };
        db.LegalDocumentVersions.Add(draft);
        await db.SaveChangesAsync();
        Assert.Equal("DOCUMENT_VERSION_CHANGED", (await Assert.ThrowsAsync<AiConsentException>(() => Service()
            .AcceptAsync(user, request with { VersionId = draft.Id }, default))).Code);
        Assert.Equal(409, (await Assert.ThrowsAsync<AiConsentException>(() => Service().AcceptAsync(user,
            request with { DisclosureId = new string('0', 64) }, default))).StatusCode);
        await Service().AcceptAsync(user, request, default);
        var next = new LegalDocumentVersion { Id = Guid.NewGuid(), DocumentId = version.DocumentId,
            Major = 2, Content = "Synthetic published revision", IsPublished = true, CreatedAt = Published.AddDays(1) };
        db.LegalDocumentVersions.Add(next);
        (await db.LegalDocuments.SingleAsync(x => x.Id == version.DocumentId)).CurrentVersionId = next.Id;
        await db.SaveChangesAsync();
        Assert.False((await Service().GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat").Accepted);
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AiConsentException>(() => Service().AcceptAsync(user,
            request with { IdempotencyKey = Guid.NewGuid() }, default))).StatusCode);
    }

    [Fact]
    public async Task Provider_configuration_change_invalidates_existing_receipt_even_when_document_is_unchanged()
    {
        await Configure();
        var request = await Request();
        await Service().AcceptAsync(user, request, default);
        configuration.Scopes["chat"].Providers = ["another-synthetic-provider"];
        var status = (await Service().GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat");
        Assert.NotEqual(request.DisclosureId, status.DisclosureId);
        Assert.False(status.Accepted);
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AiConsentException>(() => Service().AcceptAsync(user,
            request with { IdempotencyKey = Guid.NewGuid() }, default))).StatusCode);
    }

    [Fact]
    public async Task Withdrawal_covers_all_active_locales_and_reacceptance_is_a_new_receipt()
    {
        await Configure("es");
        await Configure("en");
        var request = await Request("es");
        var receipt = await Service().AcceptAsync(user, request, default);
        await Service().AcceptAsync(user, await Request("en"), default);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().WithdrawAsync(Guid.NewGuid(), receipt.Id, default));
        await Service().WithdrawAsync(user, receipt.Id, default);
        await Service().WithdrawAsync(user, receipt.Id, default);
        Assert.All(await db.LegalDocumentAcceptances.ToListAsync(), row => Assert.NotNull(row.WithdrawnAt));
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsureUserAsync(user, "chat", default))).StatusCode);
        Assert.NotNull((await Service().AcceptAsync(user, request, default)).WithdrawnAt);
        var again = await Service().AcceptAsync(user, request with { IdempotencyKey = Guid.NewGuid() }, default);
        Assert.NotEqual(receipt.Id, again.Id);
        await Service().EnsureUserAsync(user, "chat", default);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("document")]
    [InlineData("unapproved")]
    [InlineData("unconfigured")]
    [InlineData("locale")]
    public async Task Superseded_or_unavailable_status_retains_own_latest_withdrawal_receipt(string change)
    {
        var version = await Configure();
        var first = await Service().AcceptAsync(user, await Request(), default);
        var latest = await Service().AcceptAsync(user, await Request(), default);
        Assert.NotEqual(first.Id, latest.Id);
        var expectedLatest = await db.LegalDocumentAcceptances.Where(x => x.UserId == user)
            .OrderByDescending(x => x.AcceptedAt).ThenByDescending(x => x.Id).FirstAsync();
        var locale = "es";
        switch (change)
        {
            case "provider": configuration.Scopes["chat"].Providers = ["changed-synthetic-provider"]; break;
            case "document":
                var replacement = new LegalDocumentVersion { Id = Guid.NewGuid(), DocumentId = version.DocumentId,
                    Major = 2, IsPublished = true, Content = "Synthetic revision", CreatedAt = Published.AddDays(1) };
                db.LegalDocumentVersions.Add(replacement);
                (await db.LegalDocuments.SingleAsync(x => x.Id == version.DocumentId)).CurrentVersionId = replacement.Id;
                await db.SaveChangesAsync(); break;
            case "unapproved": configuration.Scopes["chat"].Approved = false; break;
            case "unconfigured": configuration.Scopes.Remove("chat"); break;
            case "locale": locale = "en"; break;
        }
        var status = (await Service().GetStatusAsync(user, locale, default)).Scopes.Single(s => s.Scope == "chat");
        Assert.False(status.Accepted);
        Assert.Equal(expectedLatest.Id, status.ReceiptId);
        Assert.Equal(expectedLatest.AcceptedAt, status.AcceptedAt);
        Assert.Null((await Service().GetStatusAsync(Guid.NewGuid(), locale, default)).Scopes.Single(s => s.Scope == "chat").ReceiptId);
        await Service().WithdrawAsync(user, status.ReceiptId!.Value, default);
        var withdrawn = (await Service().GetStatusAsync(user, locale, default)).Scopes.Single(s => s.Scope == "chat");
        Assert.Null(withdrawn.ReceiptId);
        Assert.Null(withdrawn.AcceptedAt);
        Assert.False(withdrawn.Accepted);
        Assert.All(await db.LegalDocumentAcceptances.Where(x => x.UserId == user).ToListAsync(), row => Assert.NotNull(row.WithdrawnAt));
    }

    [Fact]
    public async Task Background_subject_uses_patient_owner_not_requesting_operator_id()
    {
        await Configure();
        var patient = new PatientProfile { Id = Guid.NewGuid(), UserId = user, FirstName = "Synthetic", LastName = "Fixture" };
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        await Service().AcceptAsync(user, await Request(), default);
        await Service().EnsurePatientAsync(patient.Id, "chat", default);
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service().EnsurePatientAsync(Guid.NewGuid(), "chat", default))).StatusCode);
    }

    [Fact]
    public async Task Public_document_exposes_snapshot_id_and_time_even_when_draft_changes_document_timestamp()
    {
        var mediator = Substitute.For<IMediator>();
        var id = Guid.NewGuid();
        mediator.Send(Arg.Any<GetLegalDocumentQuery>(), Arg.Any<CancellationToken>()).Returns(new LegalDocumentDetailDto(
            Guid.NewGuid(), "synthetic", "Test", "1.0", "1.1", true,
            [new(id, 1, 0, "1.0", true, "Synthetic", null, Published, true)], Published.AddDays(5)));
        var result = await new LegalDocumentsController(mediator).GetPublished("synthetic", default);
        var dto = Assert.IsType<PublishedLegalDocumentDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(id, dto.VersionId);
        Assert.Equal(Published, dto.PublishedAt);
    }

    [Fact]
    public async Task Resource_gate_stops_before_model_binding_or_sse_and_preserves_authenticated_erp_flow()
    {
        var consent = Substitute.For<ILegalConsentService>();
        consent.EnsureUserAsync(user, "chat", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AiConsentException("CONSENT_REQUIRED", "chat", 403)));
        using var services = new ServiceCollection().AddSingleton(consent).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, User = Principal(user, "app") };
        var action = new ActionContext(http, new(), new(), new ModelStateDictionary());
        var context = new ResourceExecutingContext(action, [], []);
        var called = false;
        ResourceExecutionDelegate next = () => { called = true; return Task.FromResult(new ResourceExecutedContext(action, [])); };
        await new RequireAiConsentAttribute("chat").OnResourceExecutionAsync(context, next);
        Assert.False(called);
        Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        http.User = Principal(user, "erp");
        await new RequireAiConsentAttribute("chat").OnResourceExecutionAsync(new(action, [], []), next);
        Assert.True(called);
    }

    [Fact]
    public async Task Outgoing_handler_blocks_unmapped_background_calls_and_checks_known_subject_before_transport()
    {
        var consent = Substitute.For<ILegalConsentService>();
        var patient = Guid.NewGuid();
        consent.EnsurePatientAsync(patient, "patient-ai", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AiConsentException("CONSENT_REQUIRED", "patient-ai", 403)));
        using var services = new ServiceCollection().AddSingleton(consent).BuildServiceProvider();
        var transport = new CountTransport();
        using var client = new HttpClient(new AiConsentHttpHandler(services.GetRequiredService<IServiceScopeFactory>(), new HttpContextAccessor())
            { InnerHandler = transport });
        await Assert.ThrowsAsync<AiConsentException>(() => client.PostAsync("https://synthetic.invalid/internal/content/translate", new StringContent("test")));
        var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid/internal/program/assignment-narrative");
        request.Options.Set(AiConsentSubject.Key, new("patient-ai", PatientId: patient));
        await Assert.ThrowsAsync<AiConsentException>(() => client.SendAsync(request));
        Assert.Equal(0, transport.Count);
        await consent.Received(1).EnsurePatientAsync(patient, "patient-ai", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mixed_app_and_erp_audiences_cannot_bypass_resource_or_outgoing_consent_gates()
    {
        var consent = Substitute.For<ILegalConsentService>();
        consent.EnsureUserAsync(user, "chat", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AiConsentException("CONSENT_REQUIRED", "chat", 403)));
        using var services = new ServiceCollection().AddSingleton(consent).BuildServiceProvider();
        var principal = Principal(user, "erp");
        ((ClaimsIdentity)principal.Identity!).AddClaim(new("aud", "app"));
        var http = new DefaultHttpContext { RequestServices = services, User = principal };
        var action = new ActionContext(http, new(), new(), new ModelStateDictionary());
        var context = new ResourceExecutingContext(action, [], []);
        var called = false;
        await new RequireAiConsentAttribute("chat").OnResourceExecutionAsync(context,
            () => { called = true; return Task.FromResult(new ResourceExecutedContext(action, [])); });
        Assert.False(called);
        Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        var accessor = new HttpContextAccessor { HttpContext = http };
        var transport = new CountTransport();
        using var client = new HttpClient(new AiConsentHttpHandler(services.GetRequiredService<IServiceScopeFactory>(), accessor)
            { InnerHandler = transport });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid/chat");
        request.Options.Set(AiConsentSubject.Key, new("chat", UserId: user));
        await Assert.ThrowsAsync<AiConsentException>(() => client.SendAsync(request));
        Assert.Equal(0, transport.Count);
        await consent.Received(2).EnsureUserAsync(user, "chat", Arg.Any<CancellationToken>());
        accessor.HttpContext = null;
    }

    [Fact]
    public void Food_upload_requires_authentication_and_a_food_scope_gate()
    {
        var method = typeof(FoodAiController).GetMethod(nameof(FoodAiController.Analyze))!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(RequireAiConsentAttribute), true));
    }

    private static ClaimsPrincipal Principal(Guid id, string audience) => new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, id.ToString()), new("aud", audience)], "test"));
    private sealed class CountTransport : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Count++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }
    }
}
