using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoppAddresd.Infrastructure.Services;

public sealed class AssessmentProfileAnalysis(HttpClient http, AppDbContext db,
    IOptions<AiServiceSettings> settings, ILogger<AssessmentProfileAnalysis> logger) : IAssessmentProfileAnalysis
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AssessmentProfileGuidance?> GetAsync(Guid patientId, Guid userId, string language, bool generate, CancellationToken ct)
    {
        var results = await db.HealthTestResults.AsNoTracking()
            .Where(r => r.Evaluation!.PatientId == patientId && r.Evaluation.Status == HealthTestEvaluationStatus.completed)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(250)
            .Select(r => new { r.Id, r.EvaluationId, r.Code, r.Label, r.Value, r.Qualifier, r.Severity, r.ResultType })
            .ToListAsync(ct);
        if (results.Count == 0) return null;
        var latest = results.GroupBy(r => r.Code).Select(g => g.First()).ToArray();
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("guidance-v1:" + JsonSerializer.Serialize(latest, Json)))).ToLowerInvariant();
        var saved = await db.AssessmentProfileInsights.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PatientId == patientId && x.SourceRevision == revision && x.Language == language, ct);
        if (saved is not null) return Read(saved);
        if (!generate) return null;
        AssessmentProfileGuidance guidance;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, settings.Value.BaseUrl.TrimEnd('/') + "/internal/assessments/analyze");
            request.Headers.Add("X-Internal-Key", settings.Value.InternalApiKey);
            request.Content = JsonContent.Create(new { language, results = latest.Select(r => new {
                code = r.Code, label = r.Label, value = r.Value,
                qualifier = r.ResultType == HealthTestResultType.subscale ? null : r.Qualifier,
                severity = r.ResultType == HealthTestResultType.subscale ? null : r.Severity?.ToString(),
            }) });
            using (request)
            using (var response = await http.SendAsync(request, ct))
            {
                response.EnsureSuccessStatusCode();
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = doc.RootElement;
                var priorities = root.GetProperty("priorities").EnumerateArray().Select(item => new AssessmentGuidancePriority(
                    item.GetProperty("title").GetString()!, item.GetProperty("explanation").GetString()!,
                    item.GetProperty("action").GetString()!, item.GetProperty("evidence_codes").EnumerateArray().Select(v => v.GetString()!).ToArray())).ToArray();
                var codes = latest.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
                if (priorities.Length is < 1 or > 3 || priorities.Any(p => p.EvidenceCodes.Count == 0 || p.EvidenceCodes.Any(c => !codes.Contains(c))))
                    throw new JsonException("Referencias del análisis inválidas.");
                guidance = new(root.GetProperty("summary").GetString()!, priorities,
                    root.GetProperty("encouragement").GetString()!, root.GetProperty("questions").EnumerateArray().Select(v => v.GetString()!).ToArray(), DateTime.UtcNow, revision);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Análisis de perfil no disponible: exceptionType={ExceptionType}.", ex.GetType().Name);
            throw new AssessmentAnalysisUnavailableException();
        }
        var insight = new AssessmentProfileInsight { PatientId = patientId, RequestedBy = userId, SourceRevision = revision,
            Language = language, ContentJson = JsonSerializer.Serialize(guidance, Json), GeneratedAt = guidance.GeneratedAt };
        db.AssessmentProfileInsights.Add(insight);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_assessment_insight_source_language" })
        {
            db.Entry(insight).State = EntityState.Detached;
            var winner = await db.AssessmentProfileInsights.AsNoTracking().SingleAsync(x => x.PatientId == patientId && x.SourceRevision == revision && x.Language == language, ct);
            return Read(winner);
        }
        return guidance;
    }

    private static AssessmentProfileGuidance Read(AssessmentProfileInsight saved)
        => JsonSerializer.Deserialize<AssessmentProfileGuidance>(saved.ContentJson, Json)!;
}
