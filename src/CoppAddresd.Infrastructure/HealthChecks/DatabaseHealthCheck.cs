using CoppAddresd.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoppAddresd.Infrastructure.HealthChecks;

/// <summary>
/// Health check de conectividad con PostgreSQL. AddDbContextCheck requiere el
/// paquete Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
/// (no incluido en el shared framework de .NET 10), así que se usa un check
/// propio con CanConnectAsync: sin dependencias NuGet adicionales.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL conectado")
            : HealthCheckResult.Unhealthy("No se pudo conectar a PostgreSQL");
    }
}