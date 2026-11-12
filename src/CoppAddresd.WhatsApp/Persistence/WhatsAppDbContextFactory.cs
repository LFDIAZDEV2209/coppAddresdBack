using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.WhatsApp.Persistence;

public sealed class WhatsAppDbContextFactory : IDesignTimeDbContextFactory<WhatsAppDbContext>
{
    public WhatsAppDbContext CreateDbContext(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(root, "src/CoppAddresd.Api")) && Directory.GetParent(root) is { } parent)
            root = parent.FullName;
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "src/CoppAddresd.Api/appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(root, "src/CoppAddresd.Api/appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configura ConnectionStrings__DefaultConnection.");
        return new(new DbContextOptionsBuilder<WhatsAppDbContext>()
            .UseNpgsql(connection, x => x.MigrationsHistoryTable("__ef_migrations_history", "whatsapp")).Options);
    }
}
