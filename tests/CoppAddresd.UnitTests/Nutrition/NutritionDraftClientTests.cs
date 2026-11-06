using System.Net;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionDraftClientTests
{
    private static NutritionDraftRequest Request => new(new(1850, 217, 93, 68, 26, 2500), ["avena"],
        [new("des:0", "Avena", [new("avena", 40)])]);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => action(request, ct);
    }
    private static NutritionDraftClient Client(HttpMessageHandler handler) => new(new HttpClient(handler),
        Options.Create(new AiServiceSettings { BaseUrl = "http://nutrition.test", InternalApiKey = "clave-test" }));

    [Fact]
    public async Task WireContract_UsesInternalPathHeaderAndSnakeCase_WithoutPatientIdentity()
    {
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("/internal/nutrition/plan-draft", request.RequestUri!.AbsolutePath);
            Assert.Equal("clave-test", Assert.Single(request.Headers.GetValues("X-Internal-Key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(2500, body.RootElement.GetProperty("targets").GetProperty("water_ml").GetInt32());
            Assert.Equal("avena", body.RootElement.GetProperty("menus")[0].GetProperty("items")[0].GetProperty("food_id").GetString());
            Assert.False(body.RootElement.TryGetProperty("patient_id", out _));
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new NutritionDraftResponse(
                new("ES", "EN"), new("ES", "EN"), Request.Menus, "llm"), NutritionDraftClient.Json)) };
        });
        var response = await Client(handler).GenerateAsync(Request, CancellationToken.None);
        Assert.Equal("llm", response.Source);
        Assert.Equal("des:0", Assert.Single(response.Menus).Key);
        Assert.Equal(new DraftFood("avena", 40), Assert.Single(response.Menus[0].Items));
    }

    [Theory]
    [InlineData(503, "proveedor caído")] [InlineData(200, "{json inválido")]
    public async Task RemoteFailure_ReturnsExplicitDeterministicFallback(int status, string content)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(content) }));
        var request = Request;
        var response = await Client(handler).GenerateAsync(request, CancellationToken.None);
        Assert.Equal("deterministic_fallback", response.Source); Assert.Same(request.Menus, response.Menus);
    }

    [Fact]
    public async Task RequestCancellation_IsNotConvertedToFallback()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var handler = new Handler((_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GenerateAsync(Request, cts.Token));
    }

    [Fact]
    public async Task WireContract_MeasuredProfileAndRules_AreBilingualContextWithoutPatientIdentifiers()
    {
        var request = Request with
        {
            Menus = new[] { "des", "alm", "mer", "cen" }.Select(m => new DraftMenu(m + ":0", "Avena", [new("avena", 40)])).ToArray(),
            ClinicalProfile = new("nutrition-engine-v1", new Dictionary<string, DraftMeasuredField>
            {
                ["hba1c"] = new(5.9, "%", "clinical_measurements", "hba1c", new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc)),
                ["bodyFatPercent"] = new(null, "%", "missing", null, null)
            }, [new("engine-1", "info", "Revisar fibra y carbohidratos", false)], ["sleepRF"])
        };
        using var handler = new Handler(async (call, ct) =>
        {
            var json = await call.Content!.ReadAsStringAsync(ct);
            using var body = JsonDocument.Parse(json);
            var profile = body.RootElement.GetProperty("clinical_profile");
            Assert.Equal(5.9, profile.GetProperty("fields").GetProperty("hba1c").GetProperty("value").GetDouble());
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("fields").GetProperty("bodyFatPercent").GetProperty("value").ValueKind);
            Assert.Equal("info", profile.GetProperty("applied_rules")[0].GetProperty("severity").GetString());
            Assert.DoesNotContain("patient_id", json); Assert.DoesNotContain("evaluation_id", json);
            if (Environment.GetEnvironmentVariable("COP_NUTRITION_DRAFT_CAPTURE_PATH") is { Length: > 0 } path)
                await File.WriteAllTextAsync(path, json, ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new NutritionDraftResponse(
                new("ES", "EN"), new("ES", "EN"), request.Menus, "llm"), NutritionDraftClient.Json)) };
        });
        Assert.Equal("llm", (await Client(handler).GenerateAsync(request, CancellationToken.None)).Source);
    }
}
