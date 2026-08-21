using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Helpers de integración: fábrica de DbContext aislado y repositorios reales
/// sobre la base de prueba. Cada operación usa su propio contexto (simula el
/// scoping por request); los flujos atómicos (webhook) comparten un contexto.
/// </summary>
public sealed class TelemedicineTestContext
{
    private readonly string _connectionString;

    public TelemedicineTestContext(string connectionString)
        => _connectionString = connectionString;

    /// <summary>Nuevo DbContext con la configuración de producción (retry + historial aislado).</summary>
    public TelemedicineDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TelemedicineDbContext>()
            .UseNpgsql(_connectionString, n =>
            {
                n.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                n.MigrationsHistoryTable("__ef_migrations_history", "tele");
            })
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TelemedicineDbContext(options);
    }
}
