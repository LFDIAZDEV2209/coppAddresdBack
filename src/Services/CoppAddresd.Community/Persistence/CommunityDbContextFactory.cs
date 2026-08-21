using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoppAddresd.Community.Persistence;

/// <summary>
/// Factory de diseño para `dotnet ef` — permite generar/aplicar migraciones
/// del schema community sin levantar el servicio (usa la cadena de conexión
/// de appsettings.Development.json).
/// </summary>
public sealed class CommunityDbContextFactory : IDesignTimeDbContextFactory<CommunityDbContext>
{
    public CommunityDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "community"))
            .Options;

        return new CommunityDbContext(options);
    }
}
