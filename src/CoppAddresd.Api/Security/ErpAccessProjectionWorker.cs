using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Api.Security;

/// <summary>Recupera transiciones confirmadas por Auth aunque la petición original se interrumpa.</summary>
public sealed class ErpAccessProjectionWorker(
    IServiceScopeFactory scopes,
    ILogger<ErpAccessProjectionWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var auth = scope.ServiceProvider.GetRequiredService<IErpAccessClient>();
                var projection =
                    scope.ServiceProvider.GetRequiredService<IProfessionalAccessProjectionRepository>();
                foreach (var operation in await auth.PendingAsync(null, stoppingToken))
                {
                    if (
                        await projection.ApplyAsync(
                            operation.EmployeeId,
                            operation.UserId,
                            operation.Status,
                            operation.SessionVersion,
                            stoppingToken
                        )
                    )
                        await auth.CompleteAsync(operation.Id, stoppingToken);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "No se pudo reconciliar el acceso ERP; se reintentará."
                );
            }
        }
    }
}
