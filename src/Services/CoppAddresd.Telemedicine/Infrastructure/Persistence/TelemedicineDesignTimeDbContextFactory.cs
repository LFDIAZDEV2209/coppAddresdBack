using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoppAddresd.Telemedicine.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para que <c>dotnet ef migrations</c> pueda construir el
/// <see cref="TelemedicineDbContext"/> sin levantar la aplicación web. La
/// cadena de conexión se lee de variables de entorno / user-secrets en lugar
/// de hardcodearse.
/// </summary>
public sealed class TelemedicineDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<TelemedicineDbContext>
{
    private const string ConnectionStringEnvVar = "TELEMEDICINE_CONNECTION";

    public TelemedicineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? "Host=localhost;Port=5432;Database=coppaddresd;Username=app_user;Password=CoppAddresdDev!2026;Timeout=30;CommandTimeout=60";

        var options = new DbContextOptionsBuilder<TelemedicineDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "tele"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TelemedicineDbContext(options);
    }
}
