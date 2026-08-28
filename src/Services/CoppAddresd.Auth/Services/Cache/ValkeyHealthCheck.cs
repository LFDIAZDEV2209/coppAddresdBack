using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CoppAddresd.Auth.Services.Cache;

/// <summary>
/// Health check del caché del Auth Service: PING contra Valkey. Registrado
/// con <c>failureStatus: Degraded</c> — un Valkey caído degrada el componente
/// en /health sin tumbar el servicio (que opera vía fail-open a PostgreSQL).
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
                "Valkey inalcanzable: el Auth Service sigue operativo",
                ex
            );
        }
    }
}
