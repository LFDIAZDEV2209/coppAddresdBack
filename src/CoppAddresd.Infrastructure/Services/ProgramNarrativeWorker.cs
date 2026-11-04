using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Worker de narrativa asincrónica; los reintentos no bloquean el submit ni la aplicación del plan.</summary>
public sealed class ProgramNarrativeWorker(IProgramNarrativeQueue queue, IServiceScopeFactory scopes,
    IOptionsMonitor<ProgramAssignmentSettings> settings, ILogger<ProgramNarrativeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.WhenAll(RecoverAsync(stoppingToken), ConsumeAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProgramNarrativeRecovery>().RunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Program.NarrativeRecoveryFailed"); }
            await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(settings.CurrentValue.JobTickMinutes, 1, 60)), ct);
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        await foreach (var work in queue.ReadAllAsync(ct))
        {
            var id = work.RecommendationId;
            try
            {
                var attempts = Math.Clamp(settings.CurrentValue.NarrativeMaxAttempts, 1, 5);
                for (var attempt = 1; attempt <= attempts; attempt++)
                {
                    using var scope = scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ProgramNarrativeProcessor>();
                    if (await processor.ProcessAsync(id, ct, work.Regenerate)) break;
                    if (attempt == attempts)
                        logger.LogWarning("Program.NarrativeRetriesExhausted RecommendationId={RecommendationId}", id);
                    else
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);
                }
            }
            finally { queue.Complete(id); }
        }
    }
}
