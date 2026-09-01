using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CoppAddresd.Telemedicine.Infrastructure.Cache;

/// <summary>
/// Health check del caché de Telemedicina: PING contra Valkey. Registrado con
/// <c>failureStatus: Degraded</c> — un Valkey caído degrada el componente en
/// /health sin tumbar el microservicio (fail-open a las fuentes).
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
            return HealthCheckResult.Degraded("Valkey inalcanzable: el micro sigue operativo", ex);
        }
    }
}
