using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Localization;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

/// <summary>El catálogo compartido conserva las fuentes de los editores ERP y clientes antiguos.</summary>
public sealed class SharedSpecialtiesLocalizationHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["Medicina familiar"] = "Family medicine", ["Medicina"] = "Medicine", ["Atención primaria"] = "Primary care",
        ["Nutrición"] = "Nutrition", ["Bienestar"] = "Wellness", ["Revisión del plan nutricional"] = "Nutrition plan review",
    };

    private sealed class Translator : IContentTranslationService
    {
        public List<string[]> Calls { get; } = [];
        public Task<IReadOnlyDictionary<string, string>> TranslateAsync(IReadOnlyCollection<string> sources, string language, CancellationToken ct, ContentTranslationSubject? subject = null)
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal("en", language);
            Calls.Add(sources.ToArray());
            return Task.FromResult<IReadOnlyDictionary<string, string>>(sources.ToDictionary(source => source, source => English[source], StringComparer.Ordinal));
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var audience = Request.Headers["X-Test-Audience"].ToString();
            if (audience.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "11111111-1111-4111-8111-111111111111"), new Claim("aud", audience)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Translator Translations { get; } = new();
        public IMediator Mediator { get; } = Substitute.For<IMediator>();
        public IReadOnlyList<SpecialtyDto> Sources { get; } =
        [
            new(Guid.Parse("22222222-2222-4222-8222-222222222222"), "FAMILY_MEDICINE", "Medicina familiar", "Medicina", "Atención primaria", true),
            new(Guid.Parse("33333333-3333-4333-8333-333333333333"), "NUTRITION", "Nutrición", "Bienestar", "Revisión del plan nutricional", false),
        ];
        public HttpClient Client { get; private set; } = null!;
        private WebApplication application = null!;

        public static async Task<Harness> CreateAsync()
        {
            var test = new Harness();
            test.Mediator.Send(Arg.Any<ListSpecialtiesQuery>(), Arg.Any<CancellationToken>()).Returns(test.Sources);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            var services = builder.Services;
            services.AddSingleton(test.Mediator); services.AddSingleton(Substitute.For<ICurrentContext>());
            services.AddSingleton<IContentTranslationService>(test.Translations);
            services.AddScoped<PatientContentLocalization>(); services.AddScoped<PatientContentLocalizationFilter>();
            services.AddAuthentication("SharedCatalogTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("SharedCatalogTest", _ => { });
            services.AddAuthorization();
            services.AddControllers(options => options.Filters.Add<PatientContentLocalizationFilter>())
                .AddApplicationPart(typeof(ProfessionalCatalogsController).Assembly);
            test.application = builder.Build();
            test.application.UseMiddleware<ExceptionHandlingMiddleware>(); test.application.UseRouting();
            test.application.UseAuthentication(); test.application.UseAuthorization(); test.application.MapControllers();
            await test.application.StartAsync(); test.Client = test.application.GetTestClient();
            return test;
        }

        public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }
    }

    [Theory]
    [InlineData("erp", null, null, "es")]
    [InlineData("erp", "en", null, "es")]
    [InlineData("erp", null, "en", "es")]
    [InlineData("erp", "en", "en", "es")]
    [InlineData("app", null, null, "es")]
    [InlineData("app", "es", null, "es")]
    [InlineData("app", "en", null, "en")]
    [InlineData("app", null, "en", "en")]
    [InlineData("app", "en", "es", "es")]
    [InlineData("app", "es", "en", "en")]
    [InlineData("app", "es-CO;q=0.4,EN-us;q=0.9", null, "en")]
    [InlineData("app", "fr", null, "es")]
    public async Task SharedRoute_TranslatesOnlyExplicitPatientRequests_AndPreservesEditorSources(
        string audience, string? selectedHeader, string? query, string expectedLanguage)
    {
        await using var test = await Harness.CreateAsync();
        var before = JsonSerializer.Serialize(test.Sources, Json);
        using var request = Request(audience, selectedHeader, query);
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedLanguage, Assert.Single(response.Content.Headers.ContentLanguage));
        Assert.Equal(3, response.Headers.Vary.Count);
        Assert.Contains("Accept-Language", response.Headers.Vary);
        Assert.Contains("X-App-Language", response.Headers.Vary);
        Assert.Contains("Authorization", response.Headers.Vary);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var expected = expectedLanguage == "en" ? test.Sources.Select(source => source with
        { Name = English[source.Name], Category = English[source.Category], Description = English[source.Description!] }).ToArray() : test.Sources;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, Json), actual));
        Assert.Equal(before, JsonSerializer.Serialize(test.Sources, Json));
        Assert.Equal(expectedLanguage == "en" ? 1 : 0, test.Translations.Calls.Count);
        if (expectedLanguage == "en")
        {
            var inputs = Assert.Single(test.Translations.Calls);
            Assert.Equal(6, inputs.Length);
            Assert.DoesNotContain("FAMILY_MEDICINE", inputs); Assert.DoesNotContain("NUTRITION", inputs);
        }
        await test.Mediator.Received(1).Send(Arg.Any<ListSpecialtiesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PatientProjection_DoesNotContaminateTheNextErpEditorRead()
    {
        await using var test = await Harness.CreateAsync();
        using var patient = Request("app", "en", null);
        using var patientResponse = await test.Client.SendAsync(patient);
        Assert.Equal("Family medicine", JsonNode.Parse(await patientResponse.Content.ReadAsStringAsync())![0]!["name"]!.GetValue<string>());
        using var editor = Request("erp", "en", "en");
        using var editorResponse = await test.Client.SendAsync(editor);
        Assert.Equal(HttpStatusCode.OK, editorResponse.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(test.Sources, Json), JsonNode.Parse(await editorResponse.Content.ReadAsStringAsync())));
        Assert.Equal("Medicina familiar", test.Sources[0].Name);
        Assert.Single(test.Translations.Calls);
    }

    [Fact]
    public async Task AnonymousSharedRead_IsUnauthorized_WithoutCallingTheTranslator()
    {
        await using var test = await Harness.CreateAsync();
        using var response = await test.Client.GetAsync("/api/v1/specialties?language=en");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(test.Translations.Calls);
        await test.Mediator.DidNotReceiveWithAnyArgs().Send(default(ListSpecialtiesQuery)!, default);
    }

    private static HttpRequestMessage Request(string audience, string? selectedHeader, string? query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/specialties" + (query is null ? "" : "?language=" + query));
        request.Headers.Add("X-Test-Audience", audience);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        if (selectedHeader is not null) request.Headers.TryAddWithoutValidation("X-App-Language", selectedHeader);
        return request;
    }
}
