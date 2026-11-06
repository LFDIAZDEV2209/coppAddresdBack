using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.Localization;

public sealed class ContentTranslationServiceTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return send(request, ct); }
    }
    private static ContentTranslationService Service(Handler handler) => new(new HttpClient(handler), Options.Create(new AiServiceSettings
    { BaseUrl = "http://translation.invalid/api/v1/", InternalApiKey = "unit-test-internal-key" }));
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task HttpContract_DeduplicatesNonemptySources_PreservesNumbers_AndMapsByIdNotOrder()
    {
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://translation.invalid/internal/content/translate", request.RequestUri!.AbsoluteUri);
            Assert.Equal("unit-test-internal-key", Assert.Single(request.Headers.GetValues("X-Internal-Key")));
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct)).RootElement;
            Assert.Equal("en", body.GetProperty("target_language").GetString());
            var items = body.GetProperty("items"); Assert.Equal(2, items.GetArrayLength());
            Assert.Equal("0", items[0].GetProperty("id").GetString());
            Assert.Equal("Toma 2 cápsulas en 24 h", items[0].GetProperty("text").GetString());
            Assert.Equal("Peso 82,5 kg", items[1].GetProperty("text").GetString());
            return Json("""{"target_language":"en","items":[{"id":"1","translation":"Weight 82.5 kg"},{"id":"0","translation":"Take 2 capsules in 24 h"}]}""");
        });
        var sources = new[] { "Toma 2 cápsulas en 24 h", "Peso 82,5 kg", "Toma 2 cápsulas en 24 h", "", "  " };
        var result = await Service(handler).TranslateAsync(sources, "en", CancellationToken.None);
        Assert.Equal(2, result.Count);
        Assert.Equal("Take 2 capsules in 24 h", result[sources[0]]);
        Assert.Equal("Weight 82.5 kg", result[sources[1]]);
        Assert.Equal("Peso 82,5 kg", sources[1]); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Batches_RespectItemAndCharacterLimits_WithoutLosingSources()
    {
        var handler = new Handler(async (request, ct) =>
        {
            var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement;
            var items = payload.GetProperty("items").EnumerateArray().ToArray();
            Assert.InRange(items.Length, 1, 64);
            Assert.True(items.Sum(item => item.GetProperty("text").GetString()!.Length) <= 32000);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { target_language = "en", items = items.Select(item => new
                { id = item.GetProperty("id").GetString(), translation = item.GetProperty("text").GetString() }).ToArray() }) };
        });
        var sources = Enumerable.Range(0, 70).Select(index => $"Texto {index}").Concat(Enumerable.Range(0, 5).Select(index => new string('a', 7990) + index)).ToArray();
        var result = await Service(handler).TranslateAsync(sources, "en", CancellationToken.None);
        Assert.Equal(sources.Length, result.Count); Assert.Equal(3, handler.Calls);
        Assert.All(sources, source => Assert.Equal(source, result[source]));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"target_language\":\"es\",\"items\":[{\"id\":\"0\",\"translation\":\"Texto\"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":null}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[null]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":null,\"translation\":\"Text\"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"\",\"translation\":\"Text\"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"unknown\",\"translation\":\"Text\"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"0\",\"translation\":null}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"0\",\"translation\":\"   \"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"0\",\"translation\":123}]}")]
    public async Task InvalidPayload_AlwaysReturnsRecoverableTranslationFailure(string body)
    {
        var handler = new Handler((_, _) => Task.FromResult(Json(body)));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Service(handler).TranslateAsync(["Texto"], "en", CancellationToken.None));
    }

    [Theory]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"0\",\"translation\":\"First\"},{\"id\":\"0\",\"translation\":\"Second\"}]}")]
    [InlineData("{\"target_language\":\"en\",\"items\":[{\"id\":\"0\",\"translation\":\"First\"}]}")]
    public async Task DuplicateOrIncompleteItems_AreRejected(string body)
    {
        var handler = new Handler((_, _) => Task.FromResult(Json(body)));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Service(handler).TranslateAsync(["Uno", "Dos"], "en", CancellationToken.None));
    }

    [Theory]
    [InlineData("Take 3 capsules in 24 h")]
    [InlineData("Take capsules in 24 h")]
    [InlineData("Take 2 capsules in 24 h for 7 days")]
    public async Task ChangedMissingOrInventedNumbers_AreRejected(string translated)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { target_language = "en", items = new[] { new { id = "0", translation = translated } } }) }));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Service(handler).TranslateAsync(["Toma 2 cápsulas en 24 h"], "en", CancellationToken.None));
    }

    [Fact]
    public async Task SpanishAndEmptyInput_DoNotUseHttp_InvalidInputFailsBeforeHttp()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("No network expected"));
        var service = Service(handler);
        Assert.Equal("Texto 2", (await service.TranslateAsync(["Texto 2"], "es", CancellationToken.None))["Texto 2"]);
        Assert.Empty(await service.TranslateAsync([], "en", CancellationToken.None));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => service.TranslateAsync(["Texto"], "fr", CancellationToken.None));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => service.TranslateAsync([new string('a', 8001)], "en", CancellationToken.None));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => service.TranslateAsync(null!, "en", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task HttpFailureAndProviderTimeout_AreRecoverable_CallerCancellationIsPreserved()
    {
        var failed = new Handler((_, _) => Task.FromResult(Json("{}", HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Service(failed).TranslateAsync(["Texto"], "en", CancellationToken.None));
        var timedOut = new Handler((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Service(timedOut).TranslateAsync(["Texto"], "en", CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        var cancelled = new Handler((_, _) => { cancellation.Cancel(); return Task.FromResult(Json("{}")); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(cancelled).TranslateAsync(["Texto"], "en", cancellation.Token));
        var neverCalled = new Handler((_, _) => throw new InvalidOperationException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(neverCalled).TranslateAsync(["Texto"], "en", cancellation.Token));
        Assert.Equal(0, neverCalled.Calls);
    }
}
