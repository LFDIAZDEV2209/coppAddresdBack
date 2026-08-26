using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Definición de la colección de tests de integración de la comunidad. Todos los
/// tests comparten UNA base de datos aislada por corrida (<see cref="CommunityTestDatabase"/>):
/// se crea, se migra (schema <c>community.</c>) y se elimina al terminar. No toca la base de desarrollo.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CommunityTestCollection : ICollectionFixture<CommunityTestDatabase>
{
    public const string Name = "community-integration";
}

/// <summary>
/// Base de datos de prueba aislada para el microservicio de comunidad. Requiere la
/// variable <c>COP_TEST_DB_CONNECTION</c> apuntando a la instancia de PostgreSQL (misma
/// convención que los tests de integración del backend y de telemedicina). El usuario
/// debe poder crear bases (CREATEDB). Si la variable no está definida, los tests se omiten.
/// </summary>
public sealed class CommunityTestDatabase : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";
    private string? _sourceConnection;

    /// <summary>Cadena de conexión a la BD de prueba ya creada y migrada.</summary>
    public string ConnectionString { get; private set; } = null!;

    public string DatabaseName { get; private set; } = null!;

    /// <summary>Verdadero si la BD está disponible (COP_TEST_DB_CONNECTION definida).</summary>
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        _sourceConnection = source;
        Available = true;

        var builder = new NpgsqlConnectionStringBuilder(source);
        DatabaseName = $"coppaddresd_community_test_{Guid.NewGuid():N}";
        builder.Database = "postgres";

        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // TEMPLATE template0: el template1 de este contenedor tiene mismatch de
            // collation (XX000) que bloquea CREATE DATABASE desde él.
            create.CommandText = $"CREATE DATABASE \"{DatabaseName}\" TEMPLATE template0";
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = DatabaseName;
        ConnectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(ConnectionString, n =>
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community"))
            .Options;

        await using var db = new CommunityDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!Available || _sourceConnection is null)
        {
            return;
        }

        // Libera las conexiones en pool antes de intentar eliminar la BD.
        NpgsqlConnection.ClearAllPools();

        var builder = new NpgsqlConnectionStringBuilder(_sourceConnection);
        builder.Database = "postgres";

        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}
