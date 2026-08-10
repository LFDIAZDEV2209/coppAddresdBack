using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para dotnet-ef (no requiere que la Api registre el DbContext).
/// La cadena de conexión se lee de la variable de entorno ConnectionStrings__DefaultConnection;
/// si no existe, usa un valor local de desarrollo (no se conecta al generar migraciones).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=coppaddresd;Username=app_user;Password=CHANGE_ME";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
