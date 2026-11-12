using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.WhatsApp.Reminders;

public sealed class WhatsAppReminderWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<WhatsAppReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Guid? cursor = null;
        var nextSchedule = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (clock.GetUtcNow() >= nextSchedule)
                {
                    cursor = await scope.ServiceProvider.GetRequiredService<WhatsAppReminderScheduler>().ScheduleBatchAsync(cursor, stoppingToken);
                    nextSchedule = clock.GetUtcNow().AddSeconds(30);
                }
                if (await scope.ServiceProvider.GetRequiredService<WhatsAppReminderProcessor>().ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Worker de recordatorios WhatsApp: {Cause}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
