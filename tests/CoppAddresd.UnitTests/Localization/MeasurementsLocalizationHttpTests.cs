using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Localization;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Exceptions;
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

/// <summary>Pipeline MVC/HTTP real; solo identidad y proveedores externos se reemplazan, sin BD ni IA real.</summary>
public sealed class MeasurementsLocalizationHttpTests
{
    private sealed class Translator : IContentTranslationService
    {
        public bool Unavailable { get; set; }
        public List<string[]> Calls { get; } = [];
        public Task<IReadOnlyDictionary<string, string>> TranslateAsync(IReadOnlyCollection<string> sources, string language, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add(sources.ToArray());
            if (Unavailable) throw new ContentTranslationUnavailableException();
            Assert.Equal("en", language); Assert.Equal(new[] { "Grasa corporal" }, sources);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["Grasa corporal"] = "Body fat" });
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Authenticated")) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "11111111-1111-4111-8111-111111111111"), new Claim("aud", "app")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");
        public Guid MeasurementId { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");
        public DateTimeOffset ObservedAt { get; } = new(2026, 11, 5, 14, 30, 0, TimeSpan.Zero);
        public Translator Translations { get; } = new();
        public IMediator Mediator { get; } = Substitute.For<IMediator>();
        public CursorPagedResult<MeasurementItemDto> Page { get; }
        public HttpClient Client { get; private set; } = null!;
        private WebApplication application = null!;
        private Harness()
        {
            Page = new([new(MeasurementId, "body_fat", "Grasa corporal", 23.75m, "PERCENT", "%", ObservedAt, "clinical_measurements")], "opaque-output-cursor", true);
            Mediator.Send(Arg.Any<GetMyMeasurementsQuery>(), Arg.Any<CancellationToken>()).Returns(Page);
        }
        public static async Task<Harness> CreateAsync()
        {
            var test = new Harness();
            var current = Substitute.For<ICurrentContext>(); current.UserId.Returns(test.UserId);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            var services = builder.Services;
            services.AddSingleton(test.Mediator); services.AddSingleton(current);
            services.AddSingleton<IContentTranslationService>(test.Translations);
            services.AddScoped<PatientContentLocalization>(); services.AddScoped<PatientContentLocalizationFilter>();
            services.AddAuthentication("LocalizationTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("LocalizationTest", _ => { });
            services.AddAuthorization();
            services.AddControllers(options => options.Filters.Add<PatientContentLocalizationFilter>())
                .AddApplicationPart(typeof(MyPatientProfileController).Assembly);
            test.application = builder.Build();
            test.application.UseMiddleware<ExceptionHandlingMiddleware>(); test.application.UseRouting();
            test.application.UseAuthentication(); test.application.UseAuthorization(); test.application.MapControllers();
            await test.application.StartAsync(); test.Client = test.application.GetTestClient();
            return test;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }
    }

    [Theory]
    [InlineData("en", "Body fat")]
    [InlineData("es", "Grasa corporal")]
    public async Task RealMeasurementsRoute_LocalizesWireNameOnly_AndPreservesCanonicalData(string language, string expected)
    {
        await using var test = await Harness.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/me/measurements?language={language}&pageSize=15&cursor=opaque-input&codes=body_fat,weight");
        request.Headers.Add("X-Test-Authenticated", "true");
        request.Headers.TryAddWithoutValidation("Accept-Language", language == "en" ? "es" : "en");
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(language, Assert.Single(response.Content.Headers.ContentLanguage));
        Assert.Contains("Accept-Language", response.Headers.Vary);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var root = json.RootElement;
        Assert.Equal("opaque-output-cursor", root.GetProperty("nextCursor").GetString()); Assert.True(root.GetProperty("hasNextPage").GetBoolean());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(test.MeasurementId, item.GetProperty("id").GetGuid());
        Assert.Equal("body_fat", item.GetProperty("metricCode").GetString());
        Assert.Equal(expected, item.GetProperty("metricName").GetString());
        Assert.Equal(23.75m, item.GetProperty("value").GetDecimal());
        Assert.Equal("PERCENT", item.GetProperty("unitCode").GetString()); Assert.Equal("%", item.GetProperty("unitSymbol").GetString());
        Assert.Equal(test.ObservedAt, item.GetProperty("observedAt").GetDateTimeOffset());
        Assert.Equal("clinical_measurements", item.GetProperty("source").GetString());
        Assert.Equal("Grasa corporal", test.Page.Items[0].MetricName);
        Assert.Equal(language == "en" ? 1 : 0, test.Translations.Calls.Count);
        await test.Mediator.Received(1).Send(Arg.Is<GetMyMeasurementsQuery>(query => query.UserId == test.UserId && query.PageSize == 15 && query.Cursor == "opaque-input" && query.MetricCodes!.SequenceEqual(new[] { "body_fat", "weight" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnonymousRequest_Is401BeforeControllerOrTranslator()
    {
        await using var test = await Harness.CreateAsync();
        using var response = await test.Client.GetAsync("/api/v1/me/measurements?language=en");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Empty(test.Translations.Calls);
        await test.Mediator.DidNotReceiveWithAnyArgs().Send(default(GetMyMeasurementsQuery)!, default);
    }

    [Fact]
    public async Task TranslationUnavailable_Is503WithRetryAfter_AndDoesNotReturnSpanishAsSuccess()
    {
        await using var test = await Harness.CreateAsync(); test.Translations.Unavailable = true;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/measurements?language=en");
        request.Headers.Add("X-Test-Authenticated", "true");
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter!.Delta);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CONTENT_TRANSLATION_UNAVAILABLE", json.RootElement.GetProperty("title").GetString());
        Assert.Contains("try again", json.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(json.RootElement.TryGetProperty("items", out _)); Assert.Single(test.Translations.Calls);
        Assert.Equal("Grasa corporal", test.Page.Items[0].MetricName);
    }
}
