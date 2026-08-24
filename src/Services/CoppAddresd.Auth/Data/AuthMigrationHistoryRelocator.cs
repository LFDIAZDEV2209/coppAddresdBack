using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Auth.Data;

/// <summary>
/// Recoloca el historial de migraciones de Auth desde la tabla compartida
/// <c>public."__EFMigrationsHistory"</c> hacia <c>auth.__ef_migrations_history</c>.
///
/// Auth aisló su historial (mismo patrón que Telemedicina con
/// <c>tele.__ef_migrations_history</c>) para no mezclar IDs con el backend.
/// En bases ya existentes las tablas de <c>auth.</c> están creadas y las
/// migraciones constan en la tabla <c>public</c>, pero la tabla nueva está
/// vacía: <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/>
/// intentaría volver a ejecutar <c>InitialCreate</c> y fallaría con
/// <c>42P07</c> (relación ya existe).
///
/// Idempotente: copiar un ID ya presente es un no-op. En bases nuevas (sin
/// tabla compartida ni tablas de Auth) no inserta nada y deja que EF aplique
/// todas las migraciones.
/// </summary>
public static class AuthMigrationHistoryRelocator
{
    public const string SharedHistorySchema = "public";
    public const string SharedHistoryTable = "__EFMigrationsHistory";
    public const string AuthHistorySchema = "auth";
    public const string AuthHistoryTable = "__ef_migrations_history";
    public const string DefaultProductVersion = "10.0.10";

    /// <summary>
    /// Tablas sentinela para marcar como aplicadas las migraciones cuyos
    /// objetos ya existen cuando la tabla compartida no las lista (o no
    /// existe). <c>AddRefreshTokens</c> no aparece: su <c>Up</c> está vacío
    /// y puede re-ejecutarse sin efecto.
    /// </summary>
    public static readonly IReadOnlyList<(string MigrationId, string TableName)> TableSentinels =
    [
        ("20260810221731_InitialCreate", "Permissions"),
        ("20260813200500_AddApplicationsAndUserApplications", "Applications"),
        ("20260819201840_AddOtpCodes", "OtpCodes"),
        ("20260819214445_AddScopedAssignments", "ScopedPermissionAssignments"),
        ("20260819221901_AddInvitations", "Invitations")
    ];

    /// <summary>
    /// IDs de Auth que ya constan en el historial compartido, en el mismo
    /// orden en que aparecen en <paramref name="sharedHistoryIds"/>.
    /// </summary>
    public static IReadOnlyList<string> SelectMatchingIds(
        IEnumerable<string> authMigrationIds,
        IEnumerable<string> sharedHistoryIds)
    {
        var known = authMigrationIds.ToHashSet(StringComparer.Ordinal);
        return sharedHistoryIds.Where(known.Contains).ToArray();
    }

    public static async Task RelocateAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        var authMigrationIds = dbContext.Database.GetMigrations().ToArray();
        if (authMigrationIds.Length == 0)
        {
            return;
        }

        await dbContext.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = dbContext.Database.GetDbConnection();

            await EnsureAuthHistoryTableAsync(connection, ct);

            var copied = 0;
            if (await TableExistsAsync(connection, SharedHistorySchema, SharedHistoryTable, ct))
            {
                var sharedIds = await ReadMigrationIdsAsync(
                    connection, SharedHistorySchema, SharedHistoryTable, quoted: true, ct);
                var matching = SelectMatchingIds(authMigrationIds, sharedIds);
                copied = await CopyFromSharedHistoryAsync(connection, matching, ct);
            }

            var marked = await MarkExistingObjectsAsync(connection, authMigrationIds, ct);

            if (copied > 0 || marked > 0)
            {
                logger.LogInformation(
                    "Auth migration history relocated: {Copied} copied from shared table, {Marked} marked from existing objects",
                    copied, marked);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureAuthHistoryTableAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE SCHEMA IF NOT EXISTS auth;
            CREATE TABLE IF NOT EXISTS auth.__ef_migrations_history (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
            );
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> CopyFromSharedHistoryAsync(
        DbConnection connection,
        IReadOnlyList<string> matchingIds,
        CancellationToken ct)
    {
        if (matchingIds.Count == 0)
        {
            return 0;
        }

        var inserted = 0;
        foreach (var migrationId in matchingIds)
        {
            if (await InsertIfMissingAsync(connection, migrationId, DefaultProductVersion, ct))
            {
                inserted++;
            }
        }

        return inserted;
    }

    private static async Task<int> MarkExistingObjectsAsync(
        DbConnection connection,
        IReadOnlyList<string> authMigrationIds,
        CancellationToken ct)
    {
        var known = authMigrationIds.ToHashSet(StringComparer.Ordinal);
        var marked = 0;

        foreach (var (migrationId, tableName) in TableSentinels)
        {
            if (!known.Contains(migrationId))
            {
                continue;
            }

            if (!await TableExistsAsync(connection, AuthHistorySchema, tableName, ct))
            {
                continue;
            }

            if (await InsertIfMissingAsync(connection, migrationId, DefaultProductVersion, ct))
            {
                marked++;
            }
        }

        return marked;
    }

    private static async Task<bool> InsertIfMissingAsync(
        DbConnection connection,
        string migrationId,
        string productVersion,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO auth.__ef_migrations_history ("MigrationId", "ProductVersion")
            SELECT @id, @version
            WHERE NOT EXISTS (
                SELECT 1 FROM auth.__ef_migrations_history
                WHERE "MigrationId" = @id)
            """;
        AddParameter(command, "@id", migrationId);
        AddParameter(command, "@version", productVersion);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string schema,
        string table,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = @schema
                  AND table_name = @table)
            """;
        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", table);
        var result = await command.ExecuteScalarAsync(ct);
        return result switch
        {
            true => true,
            false or null => false,
            int n => n != 0,
            long n => n != 0,
            _ => Convert.ToBoolean(result)
        };
    }

    private static async Task<IReadOnlyList<string>> ReadMigrationIdsAsync(
        DbConnection connection,
        string schema,
        string table,
        bool quoted,
        CancellationToken ct)
    {
        var qualified = quoted
            ? $"{QuoteIdent(schema)}.{QuoteIdent(table)}"
            : $"{schema}.{table}";

        await using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT "MigrationId" FROM {qualified} ORDER BY 1""";
        await using var reader = await command.ExecuteReaderAsync(ct);
        var ids = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static string QuoteIdent(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
