using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Helpers de integración: fábrica de DbContext aislado sobre la base de prueba,
/// con la misma configuración de producción (historial de migraciones en schema
/// <c>community</c>, sin convención de nombres extra: las columnas se configuran
/// explícitamente en cada IEntityTypeConfiguration).
/// </summary>
public sealed class CommunityTestContext
{
    private readonly string _connectionString;

    public CommunityTestContext(string connectionString)
        => _connectionString = connectionString;

    /// <summary>Nuevo DbContext con la configuración de producción.</summary>
    public CommunityDbContext Create()
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(_connectionString, n =>
            {
                // Program.cs usa transacciones explícitas sin estrategia de reintentos.
                // El fixture replica esa configuración también para las escrituras por par.
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community");
            })
            .Options;
        return new CommunityDbContext(options);
    }
}
