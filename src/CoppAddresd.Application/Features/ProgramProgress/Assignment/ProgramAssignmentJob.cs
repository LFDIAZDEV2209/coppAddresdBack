using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Reconciliación acotada y recuperable con claims persistidos; el reloj controla el plazo de siete días.</summary>
public sealed class ProgramAssignmentJob(IProgramAssignmentOperationsRepository repository, IPublisher publisher,
    ISender sender, IProgramAssignmentNotifier notifier, IOptionsMonitor<ProgramAssignmentSettings> settings,
    TimeProvider clock, ILogger<ProgramAssignmentJob> logger)
{
    public async Task<ProgramAssignmentJobResult> RunAsync(CancellationToken ct)
    {
        if (!settings.CurrentValue.Enabled) return new(0, 0, 0);
        var options = settings.CurrentValue;
        var now = clock.GetUtcNow().UtcDateTime;
        var recovered = 0;
        var fallbacks = 0;
        foreach (var candidate in await repository.ListRecoveryAsync(now, options.RecoveryMaxAttempts, options.JobBatchSize, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (!await repository.TryClaimRecoveryAsync(candidate.BatteryAssignmentId, now,
                options.RecoveryMaxAttempts, options.RecoveryBaseDelaySeconds, ct)) continue;
            try
            {
                await publisher.Publish(new BatteryCompletedEvent(candidate.PatientId, candidate.BatteryAssignmentId), ct);
                if (await repository.HasRecommendationForPatientAsync(candidate.PatientId, ct)) recovered++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Program.AssignmentRecoveryFailed BatteryAssignmentId={BatteryAssignmentId}", candidate.BatteryAssignmentId); }
        }
        foreach (var candidate in await repository.ListFallbacksAsync(now.AddDays(-7), options.JobBatchSize, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (candidate.CreatedAt>now.AddDays(-7)) continue;
            try
            {
                var result = await sender.Send(new ApplyAssignmentCommand(candidate.RecommendationId, UseTemplateDefaults: true), ct);
                if (result is { Replay: false }) fallbacks++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Program.AssignmentFallbackFailed RecommendationId={RecommendationId}", candidate.RecommendationId); }
        }
        var notices = await notifier.DispatchAsync(null, now, ct);
        return new(recovered, fallbacks, notices);
    }
}
