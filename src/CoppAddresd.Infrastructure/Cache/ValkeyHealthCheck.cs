using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CoppAddresd.Infrastructure.Cache;

/// <summary>
/// Health check del caché distribuido: PING contra Valkey. Registrado con
/// <c>failureStatus: Degraded</c> — un Valkey caído degrada el componente en
/// /health (HTTP 200) sin tumbar el servicio, que sigue sirviendo tráfico
/// vía fail-open a PostgreSQL. No es un punto único de fallo.
/// </summary>
public sealed class ValkeyHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy(
                multiplexer.IsConnected
                    ? "Valkey conectado"
                    : "Valkey reconectando (fail-open activo)"
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                "Valkey inalcanzable: la app sigue operativa vía PostgreSQL",
                ex
            );
        }
    }
}
