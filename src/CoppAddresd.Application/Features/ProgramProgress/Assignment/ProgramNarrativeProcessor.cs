using System.Text.Json;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Procesa una narrativa sin modificar el snapshot ni la propuesta clínica.</summary>
public sealed class ProgramNarrativeProcessor(IProgramAssignmentRepository repository,
    IProgramNarrativeClient client, ILogger<ProgramNarrativeProcessor> logger)
{
    public async Task<bool> ProcessAsync(Guid id, CancellationToken ct, bool regenerate = false)
    {
        try
        {
            var started = regenerate ? await repository.TryStartNarrativeRegenerationAsync(id, ct)
                : await repository.TryStartNarrativeAsync(id, ct);
            if (!started) return true;
            var recommendation = await repository.GetRecommendationAsync(id, ct);
            if (recommendation is null) return true;
            var snapshot = JsonSerializer.Deserialize<JsonElement>(recommendation.ProfileSnapshot);
            var response = await client.GenerateAsync(new(snapshot,
                JsonSerializer.Deserialize<JsonElement>(recommendation.Proposal), SourceRefs(snapshot))
                { SubjectPatientId = recommendation.PatientId }, ct);
            await repository.SaveNarrativeAsync(id, response.NarrativeEs, response.NarrativeEn, response.ModelName, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Program.NarrativeFailed RecommendationId={RecommendationId}", id);
            return false;
        }
    }

    /// <summary>Solo fuentes publicadas en condiciones activas del snapshot; sin referencias inventadas.</summary>
    private static IReadOnlyList<string> SourceRefs(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
            return [];
        return conditions.EnumerateArray()
            .Where(c => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("active", out var active)
                && active.ValueKind == JsonValueKind.True && c.TryGetProperty("sourceRef", out var source)
                && source.ValueKind == JsonValueKind.String)
            .Select(c => c.GetProperty("sourceRef").GetString()!.Trim())
            .Where(s => s.Length is > 0 and <= 500)
            .Distinct(StringComparer.Ordinal).Take(100).ToArray();
    }
}
