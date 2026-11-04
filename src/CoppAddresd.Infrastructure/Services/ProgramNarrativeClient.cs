using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Cliente del endpoint de narrativa del ai-service; resiliencia y correlation ID se configuran en DI.</summary>
public sealed class ProgramNarrativeClient(HttpClient http, IOptions<AiServiceSettings> settings) : IProgramNarrativeClient
{
    public async Task<ProgramNarrativeResponse> GenerateAsync(ProgramNarrativeRequest payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.InternalApiKey))
            throw new InvalidOperationException("AiService:InternalApiKey no configurada.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/program/assignment-narrative")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-Internal-Key", settings.Value.InternalApiKey);
        using var response = await http.SendAsync(request, ct);
        // No registrar body ni perfil clínico cuando falla el proveedor.
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<NarrativeEnvelope>(cancellationToken: ct);
        var es = result?.Narratives?.Es?.Trim();
        var en = result?.Narratives?.En?.Trim();
        if (string.IsNullOrWhiteSpace(es) || string.IsNullOrWhiteSpace(en) || es.Length > 2000 || en.Length > 2000)
            throw new InvalidOperationException("La narrativa debe incluir texto ES y EN.");
        // El schema Python devuelve exclusivamente narratives:{es,en}; no publica model_name.
        return new(es, en);
    }

    private sealed record NarrativeEnvelope([property: JsonPropertyName("narratives")] NarrativeTexts? Narratives);
    private sealed record NarrativeTexts([property: JsonPropertyName("es")] string? Es,
        [property: JsonPropertyName("en")] string? En);
}
