using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Cliente del endpoint admin de ejecuciones del AI Service. El frontend
/// nunca llama al AI Service directamente: este proxy es la única puerta de
/// entrada para el monitoreo admin (las 12 preguntas del plan de agentes).
/// </summary>
public sealed class AgentExecutionsQueryService(
    HttpClient httpClient,
    IOptions<AiServiceSettings> settings,
    ILogger<AgentExecutionsQueryService> logger) : IAgentExecutionsQueryService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AgentExecutionsListDto> ListAsync(
        AgentExecutionQueryOptions options,
        CancellationToken ct = default)
    {
        var endpoint = $"{settings.Value.ExecutionsEndpoint}?{options.ToQueryString()}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddInternalKeyHeader(request);
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Listado de ejecuciones rechazado: {Status} {Body}",
                    response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                return new AgentExecutionsListDto(0, []);
            }

            var payload = await response.Content.ReadFromJsonAsync<ExecutionsPageJson>(JsonOpts, ct);
            var items = payload?.Items
                .Select(ToSummary)
                .ToList() ?? [];

            return new AgentExecutionsListDto(payload?.Total ?? items.Count, items);
        }
        catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudo consultar el listado de ejecuciones del AI Service");
            return new AgentExecutionsListDto(0, []);
        }
    }

    public async Task<AgentExecutionDetailDto?> GetAsync(string executionId, CancellationToken ct = default)
    {
        var endpoint = $"{settings.Value.ExecutionsEndpoint}/{executionId}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddInternalKeyHeader(request);
            using var response = await httpClient.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Detalle de ejecución rechazado: {Status} {Body}",
                    response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<ExecutionDetailJson>(JsonOpts, ct);
            return payload is null ? null : ToDetail(payload);
        }
        catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudo consultar el detalle de la ejecución {ExecutionId}",
                executionId);
            return null;
        }
    }

    /// <summary>Autentica el canal interno backend → AI Service (X-Internal-Key).</summary>
    private void AddInternalKeyHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.InternalApiKey))
        {
            logger.LogWarning(
                "AiService:InternalApiKey no configurada — el AI Service rechazará la llamada (401/503).");
            return;
        }
        request.Headers.TryAddWithoutValidation("X-Internal-Key", settings.Value.InternalApiKey);
    }

    private static AgentExecutionSummaryDto ToSummary(ExecutionSummaryJson j) => new(
        j.Id,
        j.ThreadId,
        j.AgentTypeId,
        j.VersionId,
        j.UserId,
        j.Status,
        j.LatencyMs,
        j.TokensIn,
        j.TokensOut,
        j.Model,
        j.Error,
        j.CreatedAt,
        j.EndedAt,
        j.FeedbackRating,
        j.BestEvaluation);

    private static AgentExecutionDetailDto ToDetail(ExecutionDetailJson j) => new(
        j.Id,
        j.ThreadId,
        j.AgentTypeId,
        j.VersionId,
        j.UserId,
        j.Status,
        j.LatencyMs,
        j.TokensIn,
        j.TokensOut,
        j.Model,
        j.Error,
        j.CreatedAt,
        j.EndedAt,
        j.FeedbackRating,
        j.BestEvaluation,
        j.FeedbackComment,
        j.Input,
        j.Output,
        (j.Evaluations ?? []).Select(e => new AgentEvaluationDto(
            e.Evaluator, e.Metric, e.Score, e.Details)).ToList(),
        (j.Experiences ?? []).Select(x => new AgentExperienceLiteDto(
            x.Trigger, x.Response, x.Outcome, x.Recurrence, x.Rating)).ToList());

    // --- Contratos JSON del AI Service (snake_case) ---

    private sealed class ExecutionsPageJson
    {
        public int Total { get; set; }
        public List<ExecutionSummaryJson> Items { get; set; } = [];
    }

    private class ExecutionSummaryJson
    {
        public string Id { get; set; } = default!;
        public string? ThreadId { get; set; }
        public string AgentTypeId { get; set; } = default!;
        public string? VersionId { get; set; }
        public string? UserId { get; set; }
        public string Status { get; set; } = default!;
        public int? LatencyMs { get; set; }
        public int? TokensIn { get; set; }
        public int? TokensOut { get; set; }
        public string? Model { get; set; }
        public string? Error { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int? FeedbackRating { get; set; }
        public double? BestEvaluation { get; set; }
    }

    private sealed class ExecutionDetailJson : ExecutionSummaryJson
    {
        public string? FeedbackComment { get; set; }
        public IReadOnlyDictionary<string, object>? Input { get; set; }
        public IReadOnlyDictionary<string, object>? Output { get; set; }
        public List<EvaluationJson>? Evaluations { get; set; }
        public List<ExperienceJson>? Experiences { get; set; }
    }

    private sealed class EvaluationJson
    {
        public string Evaluator { get; set; } = default!;
        public string? Metric { get; set; }
        public double? Score { get; set; }
        public string? Details { get; set; }
    }

    private sealed class ExperienceJson
    {
        public string Trigger { get; set; } = default!;
        public string Response { get; set; } = default!;
        public string? Outcome { get; set; }
        public int Recurrence { get; set; }
        public double? Rating { get; set; }
    }
}
