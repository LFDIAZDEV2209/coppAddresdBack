using CoppAddresd.WhatsApp.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.WhatsApp.Webhook;

public sealed class WhatsAppInboundWorker(IServiceScopeFactory scopes, WhatsAppQueue queue,
    ILogger<WhatsAppInboundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (queue.TryRead(out _)) { }
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<WhatsAppInboundProcessor>().ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Worker WhatsApp: {Cause}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
