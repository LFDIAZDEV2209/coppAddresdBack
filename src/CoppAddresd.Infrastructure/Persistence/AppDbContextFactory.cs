using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para dotnet-ef (no requiere que la Api registre el DbContext).
/// La cadena de conexión se resuelve igual que la app en runtime:
/// appsettings.json (del proyecto CoppAddresd.Api) + variables de entorno
/// (ConnectionStrings__DefaultConnection tiene prioridad).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveBasePath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada. " +
                "Ejecuta dotnet ef desde el proyecto CoppAddresd.Api o define la variable " +
                "de entorno ConnectionStrings__DefaultConnection.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Busca el directorio con appsettings.json, soportando ejecutar dotnet ef
    /// desde la raíz de la solución, desde CoppAddresd.Api o desde Infrastructure.
    /// </summary>
    private static string ResolveBasePath()
    {
        var current = Directory.GetCurrentDirectory();

        var candidates = new[]
        {
            current,
            Path.Combine(current, "src", "CoppAddresd.Api"),
            Path.Combine(current, "..", "CoppAddresd.Api"),
            Path.Combine(current, "..", "src", "CoppAddresd.Api"),
            Path.Combine(current, "coppAddresdBack", "src", "CoppAddresd.Api"),
        };

        return candidates.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "appsettings.json")))
            ?? current;
    }
}
