using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using System.Net;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CoppAddresd.IntegrationTests;

public sealed class NarrativeSimulationFactAttribute : FactAttribute
{
    public NarrativeSimulationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COP_PROGRAM_NARRATIVE_SIM_URL")))
            Skip = "Requiere simulación HTTP de Postman en COP_PROGRAM_NARRATIVE_SIM_URL.";
    }
}

/// <summary>Cliente .NET real contra la simulación local del schema Python; no llama al proveedor real.</summary>
public sealed class ProgramNarrativeHttpIntegrationTests
{
    [NarrativeSimulationFact]
    public async Task ClienteReal_ContratoBilingueY503DeProveedor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ProgramNarrativeResilience>();
        services.AddHttpClient("narrative-simulation", http => http.BaseAddress = new Uri(
            Environment.GetEnvironmentVariable("COP_PROGRAM_NARRATIVE_SIM_URL")!))
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<ProgramNarrativeResilience>().Policy);
        using var provider = services.BuildServiceProvider();
        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("narrative-simulation");
        http.DefaultRequestHeaders.Add("x-mock-slug", "narrative");
        var client = new ProgramNarrativeClient(http, Options.Create(new AiServiceSettings { InternalApiKey = "simulation-only-key" }));
        var payload = new ProgramNarrativeRequest(JsonSerializer.Deserialize<JsonElement>("{\"biometrics\":[]}"),
            JsonSerializer.Deserialize<JsonElement>("{\"matchedRules\":[]}"), ["ADA 2026"]);
        var result = await client.GenerateAsync(payload, CancellationToken.None);
        Assert.Null(result.ModelName);
        Assert.NotEmpty(result.NarrativeEs); Assert.NotEmpty(result.NarrativeEn);
        // El array vacío también es válido en el schema Pydantic (no hay fuentes activas).
        await client.GenerateAsync(payload with { SourceRefs = [] }, CancellationToken.None);
        http.DefaultRequestHeaders.Add("x-mock-response-code", "503");
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.GenerateAsync(payload, CancellationToken.None));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        // El circuito conserva los fallos entre requests, después del quinto 503.
        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() => client.GenerateAsync(payload, CancellationToken.None));
    }
}
