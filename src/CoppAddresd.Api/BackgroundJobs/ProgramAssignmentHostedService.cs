using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.BackgroundJobs;

/// <summary>Pasadas acotadas del reconciliador; claims en PostgreSQL coordinan varias instancias.</summary>
public sealed class ProgramAssignmentHostedService(IServiceScopeFactory scopes,
    IOptionsMonitor<ProgramAssignmentSettings> settings, ILogger<ProgramAssignmentHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (settings.CurrentValue.Enabled)
                {
                    using var scope=scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ProgramAssignmentJob>().RunAsync(stoppingToken);
                }
                await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(settings.CurrentValue.JobTickMinutes,1,1440)),stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex,"Program.AssignmentJobFailed");
                await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);
            }
        }
    }
}
