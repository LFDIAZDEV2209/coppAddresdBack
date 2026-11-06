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
        // Una ejecución completa por instrumento: no mezclar dimensiones de un
        // intento anterior ni colapsar los códigos «total» de tests diferentes.
        var evaluationIds = await db.HealthTestEvaluations.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == HealthTestEvaluationStatus.completed)
            .GroupBy(e => e.Version!.InstrumentId)
            .Select(g => g.OrderByDescending(e => e.CompletedAt ?? e.StartedAt).ThenByDescending(e => e.StartedAt)
                .ThenByDescending(e => e.Id).Select(e => e.Id).First())
            .ToListAsync(ct);
        var results = await db.HealthTestResults.AsNoTracking()
            .Where(r => evaluationIds.Contains(r.EvaluationId))
            .OrderBy(r => r.Evaluation!.Version!.Instrument!.Code).ThenBy(r => r.Code)
            .ThenBy(r => r.ResultType).ThenBy(r => r.Id).Take(251)
            .Select(r => new { r.Id, r.EvaluationId, r.Code, r.Label, r.Value, r.Qualifier, r.Severity, r.ResultType,
                TestCode = r.Evaluation!.Version!.Instrument!.Code,
                TestName = r.Evaluation.Version.Instrument.Name, r.Evaluation.CompletedAt })
            .ToListAsync(ct);
        if (results.Count == 0) return null;
        // El límite protege al proveedor sin truncar silenciosamente un perfil.
        if (results.Count > 250) throw new AssessmentAnalysisUnavailableException();
        var pending = await db.HealthTestAssignments.AsNoTracking().CountAsync(a => a.PatientId == patientId &&
            (a.Status == HealthTestAssignmentStatus.pending || a.Status == HealthTestAssignmentStatus.in_progress), ct);
        var sources = results.Select(r => new AssessmentGuidanceSource(
            $"{r.TestCode}:{r.Code}:{r.ResultType}", r.TestCode, r.TestName, r.Code, r.Label,
            r.ResultType.ToString(), r.Value,
            r.ResultType == HealthTestResultType.subscale ? null : r.Qualifier,
            r.ResultType == HealthTestResultType.subscale ? null : r.Severity?.ToString(), r.CompletedAt)).ToArray();
        if (sources.Select(s => s.EvidenceCode).Distinct(StringComparer.Ordinal).Count() != sources.Length)
            throw new AssessmentAnalysisUnavailableException();
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "guidance-v2:" + JsonSerializer.Serialize(new { results, pending }, Json)))).ToLowerInvariant();
        var saved = await db.AssessmentProfileInsights.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PatientId == patientId && x.SourceRevision == revision && x.Language == language, ct);
        if (saved is not null) return Read(saved);
        if (!generate) return null;
        AssessmentProfileGuidance guidance;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, settings.Value.BaseUrl.TrimEnd('/') + "/internal/assessments/analyze");
            request.Headers.Add("X-Internal-Key", settings.Value.InternalApiKey);
            request.Content = JsonContent.Create(new { language, pending_assessments = pending, results = sources.Select(r => new {
                code = r.EvidenceCode, label = r.Label, value = r.Value, instrument = r.TestName,
                result_type = r.ResultType, completed_at = r.CompletedAt,
                qualifier = r.Qualifier, severity = r.Severity,
            }) });
            using (request)
            using (var response = await http.SendAsync(request, ct))
            {
                response.EnsureSuccessStatusCode();
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = doc.RootElement;
                var priorities = root.GetProperty("priorities").EnumerateArray().Select(item => new AssessmentGuidancePriority(
                    ReadText(item.GetProperty("title"), 5, 120), ReadText(item.GetProperty("explanation"), 20, 1000),
                    ReadText(item.GetProperty("action"), 10, 500), item.GetProperty("evidence_codes").EnumerateArray().Select(v => v.GetString()!).ToArray())).ToArray();
                var codes = sources.Select(r => r.EvidenceCode).ToHashSet(StringComparer.Ordinal);
                if (priorities.Length is < 1 or > 3 || priorities.Any(p => p.EvidenceCodes.Count is < 1 or > 10 || p.EvidenceCodes.Any(c => !codes.Contains(c))))
                    throw new JsonException("Referencias del análisis inválidas.");
                var questions = root.GetProperty("questions").EnumerateArray().Select(v => ReadText(v, 1, 400)).ToArray();
                if (questions.Length > 3) throw new JsonException("Demasiadas preguntas en el análisis.");
                guidance = new(ReadText(root.GetProperty("summary"), 40, 1600), priorities,
                    ReadText(root.GetProperty("encouragement"), 10, 400), questions, DateTime.UtcNow, revision, sources, pending > 0);
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

    private static string ReadText(JsonElement element, int min, int max)
    {
        var value = element.GetString()?.Trim();
        return value is not null && value.Length >= min && value.Length <= max
            ? value : throw new JsonException("Texto del análisis fuera de los límites.");
    }
}
