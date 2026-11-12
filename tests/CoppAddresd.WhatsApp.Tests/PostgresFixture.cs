using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.WhatsApp.Tests;

/// <summary>BD desechable en el PostgreSQL local: nunca modifica datos de desarrollo.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string name = "copp_whatsapp_test_" + Guid.NewGuid().ToString("N");
    private string adminConnection = "";
    public string ConnectionString { get; private set; } = "";
    public WhatsAppDbContext CreateDb() => new(new DbContextOptionsBuilder<WhatsAppDbContext>()
        .UseNpgsql(ConnectionString, pg => pg.MigrationsHistoryTable("__ef_migrations_history", "whatsapp")).Options);

    public async Task InitializeAsync()
    {
        using var designDb = new WhatsAppDbContextFactory().CreateDbContext([]);
        var configured = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? designDb.Database.GetConnectionString()!;
        var connection = new NpgsqlConnectionStringBuilder(configured);
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Los tests WhatsApp solo crean bases desechables en PostgreSQL local.");
        connection.Database = "postgres";
        adminConnection = connection.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
        connection.Database = name;
        ConnectionString = connection.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(adminConnection)) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
