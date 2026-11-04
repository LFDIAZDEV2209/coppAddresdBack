using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Consume el outbox persistido; reinicios y fallos de IA no pierden la solicitud.</summary>
public sealed class NutritionPlanGenerationWorker(IServiceScopeFactory scopes,
    ILogger<NutritionPlanGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextReconcile = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var workflow = scope.ServiceProvider.GetRequiredService<INutritionPlanWorkflow>();
                if (DateTime.UtcNow >= nextReconcile)
                {
                    await workflow.ReconcileAsync(stoppingToken);
                    nextReconcile = DateTime.UtcNow.AddMinutes(1);
                }
                if (await workflow.ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Worker de nutrición pendiente: tipo={ExceptionType}", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
