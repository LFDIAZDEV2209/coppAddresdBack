using System.Text.Json.Nodes;
using System.Security.Claims;
using CoppAddresd.Api.Localization;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

public sealed class PatientContentLocalizationFilterTests
{
    [Theory]
    [InlineData(null, null, "es")]
    [InlineData("en-US,en;q=0.8,es;q=0.4", null, "en")]
    [InlineData("ES-co;q=0.4,EN-us;q=0.9", null, "en")]
    [InlineData("en;q=0.5,ES;q=0.9", null, "es")]
    [InlineData("en;q=0,es;q=0.5", null, "es")]
    [InlineData("en;q=0,fr;q=1", null, "es")]
    [InlineData("fr-FR,en-US;q=0.8", null, "en")]
    [InlineData("en;q=oops,es;q=0.5", null, "es")]
    [InlineData("garbage;;,EN;q=0.7", null, "en")]
    [InlineData("en;q=2,es;q=0.5", null, "es")]
    [InlineData("en;q=-1,es;q=0.5", null, "es")]
    [InlineData("*", null, "es")]
    [InlineData("*;q=1,en;q=0.5", null, "es")]
    [InlineData("*;q=1,es;q=0,en;q=0.5", null, "en")]
    [InlineData("*;q=0.4,es;q=0,en;q=0", null, "es")]
    [InlineData("*;q=1,es;q=0.5,en;q=0.6", null, "en")]
    [InlineData("es", "EN", "en")]
    [InlineData("en", "es", "es")]
    [InlineData("en", "fr", "es")]
    [InlineData("es", " en ", "en")]
    public void LanguageNegotiation_RespectsExplicitChoiceCaseQualityAndMalformedTokens(string? accept, string? query, string expected)
    {
        var http = new DefaultHttpContext();
        if (accept is not null) http.Request.Headers.AcceptLanguage = accept;
        if (query is not null) http.Request.QueryString = QueryString.Create("language", query);
        Assert.Equal(expected, PatientContentLocalizationFilter.ResolveLanguage(http.Request));
    }

    [Theory]
    [InlineData("app", null, null, "es")]
    [InlineData("app", "en", null, "en")]
    [InlineData("app", " EN ", null, "en")]
    [InlineData("app", "es-CO;q=0.4,EN-us;q=0.9", null, "en")]
    [InlineData("app", "en;q=0,ES-co;q=0.9", null, "es")]
    [InlineData("app", "en;q=oops,es;q=0.5", null, "es")]
    [InlineData("app", "fr-FR,en-US;q=0.8", null, "en")]
    [InlineData("app", "es", null, "es")]
    [InlineData("app", "en", "es", "es")]
    [InlineData("app", "es", "en", "en")]
    [InlineData("app", "en", "fr", "es")]
    [InlineData("app", "fr", null, "es")]
    [InlineData("erp", "en", null, "es")]
    [InlineData("erp", "en", "en", "es")]
    [InlineData(null, "en", "en", "es")]
    public void PatientProjection_RequiresAppAudienceAndExplicitChoice_IndependentlyOfAutomaticAcceptLanguage(
        string? audience, string? header, string? query, string expected)
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(audience is null ? [] : [new Claim("aud", audience)], "test"));
        http.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";
        if (header is not null) http.Request.Headers["X-App-Language"] = header;
        if (query is not null) http.Request.QueryString = QueryString.Create("language", query);
        Assert.Equal(expected, PatientContentLocalizationFilter.ResolvePatientLanguage(http));
    }

    [Fact]
    public void ExplicitSelectedHeader_PrecedesAutomaticLanguage_ButQueryStillPrecedesHeader()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.AcceptLanguage = "en-US";
        http.Request.Headers["X-App-Language"] = "es";
        Assert.Equal("es", PatientContentLocalizationFilter.ResolveLanguage(http.Request));
        http.Request.QueryString = QueryString.Create("language", "en");
        Assert.Equal("en", PatientContentLocalizationFilter.ResolveLanguage(http.Request));
    }

    [Theory]
    [InlineData("en", "English plan")]
    [InlineData("es", "Plan clínico")]
    public async Task SuccessfulAnnotatedResponse_UsesConfiguredSerializationLanguageAndVary_WithoutChangingDto(string language, string expected)
    {
        var original = new { Name = "Plan clínico", Id = "plan-id", UserText = "Mi comentario" };
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["Plan clínico"] = "English plan" });
        var context = Context(new OkObjectResult(original), language, annotated: true);
        context.HttpContext.Response.Headers.Vary = "Origin";
        var filter = Filter(translator); var called = false;
        await filter.OnResultExecutionAsync(context, () => { called = true; return Next(context); });
        Assert.True(called); Assert.Equal(language, context.HttpContext.Response.Headers.ContentLanguage.ToString());
        Assert.Equal(new[] { "Origin", "Accept-Language", "X-App-Language", "Authorization" }, VaryTokens(context));
        if (language == "en")
        {
            var value = Assert.IsAssignableFrom<JsonNode>(((ObjectResult)context.Result).Value);
            Assert.Equal(expected, value["name"]!.GetValue<string>());
            Assert.Equal("plan-id", value["id"]!.GetValue<string>());
            Assert.Equal("Mi comentario", value["userText"]!.GetValue<string>());
            Assert.Equal(typeof(JsonNode), ((ObjectResult)context.Result).DeclaredType);
            await translator.Received(1).TranslateAsync(Arg.Is<IReadOnlyCollection<string>>(sources => sources.SequenceEqual(new[] { "Plan clínico" })), "en", CancellationToken.None);
        }
        else
        {
            Assert.Same(original, ((ObjectResult)context.Result).Value);
            await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
        }
        Assert.Equal("Plan clínico", original.Name);
    }

    [Theory]
    [InlineData("Origin, accept-language")]
    [InlineData("Accept-Language, Origin")]
    [InlineData("Origin, accept-language, X-APP-LANGUAGE, authorization")]
    [InlineData("*")]
    public async Task Vary_IsNotDuplicated_AndWildcardIsPreserved(string existing)
    {
        var context = Context(new OkObjectResult(new { id = "id" }), "es", true);
        context.HttpContext.Response.Headers.Vary = existing;
        await Filter(Substitute.For<IContentTranslationService>()).OnResultExecutionAsync(context, () => Next(context));
        if (existing == "*") Assert.Equal(existing, context.HttpContext.Response.Headers.Vary.ToString());
        else
        {
            var tokens = VaryTokens(context);
            Assert.Equal(1, tokens.Count(value => value.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(1, tokens.Count(value => value.Equals("X-App-Language", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(1, tokens.Count(value => value.Equals("Authorization", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("Origin", tokens);
        }
    }

    [Theory]
    [InlineData(false, 200)]
    [InlineData(true, 400)]
    [InlineData(true, 404)]
    public async Task UnannotatedAndFailedResponses_AreNeverTranslated(bool annotated, int status)
    {
        var translator = Substitute.For<IContentTranslationService>();
        var original = new { name = "Contenido", detail = "Detalle" };
        var context = Context(new ObjectResult(original) { StatusCode = status }, "en", annotated);
        await Filter(translator).OnResultExecutionAsync(context, () => Next(context));
        Assert.Same(original, ((ObjectResult)context.Result).Value);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Content-Language"));
        Assert.Equal(annotated, context.HttpContext.Response.Headers.ContainsKey("Vary"));
        await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
    }

    [Fact]
    public void MultipleHeaderLines_UseQualityAcrossLines_AndKeepSupportedStableTieOrder()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.AcceptLanguage = new Microsoft.Extensions.Primitives.StringValues(["fr;q=1,ES-co;q=0.4", "EN-us;q=0.8"]);
        Assert.Equal("en", PatientContentLocalizationFilter.ResolveLanguage(http.Request));
        http.Request.Headers.AcceptLanguage = "ES;q=0.8,en;q=0.8";
        Assert.Equal("es", PatientContentLocalizationFilter.ResolveLanguage(http.Request));
        http.Request.Headers.AcceptLanguage = "*;q=1,en;q=0";
        Assert.Equal("es", PatientContentLocalizationFilter.ResolveLanguage(http.Request));
    }

    [Fact]
    public async Task IncompleteTranslation_DoesNotExecuteSuccessOrRelabelSpanishAsEnglish()
    {
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>()).Returns(new Dictionary<string, string>());
        var source = new { name = "Plan clínico" }; var context = Context(new OkObjectResult(source), "en", true);
        var called = false;
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Filter(translator).OnResultExecutionAsync(context, () => { called = true; return Next(context); }));
        Assert.False(called); Assert.Same(source, ((ObjectResult)context.Result).Value);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Content-Language"));
        Assert.Equal(new[] { "Accept-Language", "X-App-Language", "Authorization" }, VaryTokens(context));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task CallerCancellation_PreventsExecutingSuccessInEitherLanguage(string language)
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var context = Context(new OkObjectResult(new { name = "Plan" }), language, true);
        context.HttpContext.RequestAborted = cancellation.Token;
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Filter(Substitute.For<IContentTranslationService>())
            .OnResultExecutionAsync(context, () => { called = true; return Next(context); }));
        Assert.False(called); Assert.False(context.HttpContext.Response.Headers.ContainsKey("Content-Language"));
    }

    private static PatientContentLocalizationFilter Filter(IContentTranslationService translator) => new(new PatientContentLocalization(translator), Options.Create(new JsonOptions()));
    private static ResultExecutingContext Context(IActionResult result, string language, bool annotated)
    {
        var http = new DefaultHttpContext(); http.Request.QueryString = QueryString.Create("language", language);
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("aud", "app")], "test"));
        var descriptor = new ActionDescriptor { EndpointMetadata = annotated ? [new LocalizePatientContentAttribute(PatientContentProfile.NutritionPlan)] : [] };
        return new(new ActionContext(http, new RouteData(), descriptor), [], result, new object());
    }
    private static Task<ResultExecutedContext> Next(ResultExecutingContext context) => Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller));
    private static string[] VaryTokens(ResultExecutingContext context) => context.HttpContext.Response.Headers.Vary
        .SelectMany(value => (value ?? "").Split(',')).Select(value => value.Trim()).ToArray();
}
