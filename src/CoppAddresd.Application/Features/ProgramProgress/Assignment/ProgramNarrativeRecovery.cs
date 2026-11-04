using System.Linq.Expressions;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Predicado compartido por la consulta SQL y las pruebas de recuperación.</summary>
public static class ProgramNarrativeEligibility
{
    public static readonly Expression<Func<ProgramAssignmentRecommendation, bool>> MissingNarrative = r =>
        ((r.State == "proposed" || r.State == "pending-narrative")
            && (r.NarrativeEs == null || r.NarrativeEn == null))
        || ((r.State == "applied" || r.State == "edited" || r.State == "fallback")
            && r.NarrativeEs == null && r.NarrativeEn == null);

    /// <summary>Consulta traducible a SQL; el debounce se aplica antes del orden estable y del límite.</summary>
    public static IQueryable<Guid> SelectPending(IQueryable<ProgramAssignmentRecommendation> source, DateTime cutoff, int limit) =>
        source.Where(MissingNarrative)
            .Where(r => r.CreatedAt <= cutoff && (r.UpdatedAt == null || r.UpdatedAt <= cutoff))
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Take(Math.Clamp(limit, 1, 500))
            .Select(r => r.Id);
}

/// <summary>Recupera trabajo perdido del canal sin reabrir decisiones clínicas ni superar el lote configurado.</summary>
public sealed class ProgramNarrativeRecovery(IProgramAssignmentRepository repository, IProgramNarrativeQueue queue,
    IOptionsMonitor<ProgramAssignmentSettings> settings, TimeProvider clock)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var options = settings.CurrentValue;
        if (!options.Enabled) return 0;
        var cutoff = clock.GetUtcNow().UtcDateTime.AddSeconds(-Math.Max(1, options.NarrativeDebounceSeconds));
        var ids = await repository.ListMissingNarrativesAsync(cutoff, options.JobBatchSize, ct);
        var queued = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (!queue.TryEnqueue(id)) break;
            queued++;
        }
        return queued;
    }
}
