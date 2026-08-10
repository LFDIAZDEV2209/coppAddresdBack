using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Pruebas de integración del activity log con PostgreSQL real.
/// Requieren la variable de entorno COP_TEST_DB_CONNECTION apuntando a la BD
/// con la migración InitialAuditSchema aplicada (p. ej. la local coppaddresd);
/// si no está definida, los tests se saltan (Assert.Skip).
/// </summary>
public sealed class AuditTriggerTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";
    private const string RowId = "22222222-2222-2222-2222-222222222222";
    private static readonly Guid ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private readonly string _schema = $"audit_test_{Guid.NewGuid():N}";
    private bool _skipped;

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
            return Task.CompletedTask;
        }

        return ExecuteAsync(async (cmd, ct) =>
        {
            cmd.CommandText = $"""
                CREATE SCHEMA {_schema};
                CREATE TABLE {_schema}.people (
                    id uuid PRIMARY KEY,
                    name text NOT NULL,
                    email text,
                    password_hash text);
                SELECT audit.attach_table_audit('{_schema}', 'people', 'id', 'password_hash');
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        });
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await ExecuteAsync(async (cmd, ct) =>
        {
            cmd.CommandText = $"""
                DROP SCHEMA IF EXISTS {_schema} CASCADE;
                DELETE FROM audit.activity_logs WHERE schema_name = '{_schema}';
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        });
    }

    [Fact]
    public async Task Crud_ConActorViaGuc_RegistraAccionesSinColumnasSensibles()
    {
        if (IsSkipped) { return; }

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT set_config('audit.actor_type', 'USER', true)
                 , set_config('audit.user_id', @userId, true)
                 , set_config('audit.user_email', 'ana@x.com', true)
                 , set_config('audit.correlation_id', 'corr-test-1', true);
            """;
        command.Parameters.Add(new NpgsqlParameter("userId", ActorUserId.ToString()));
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"""
            INSERT INTO {_schema}.people VALUES ('{RowId}', 'Ana', 'ana@x.com', 'secret-1');
            UPDATE {_schema}.people SET name = 'Ana P.', email = 'ana.p@x.com' WHERE id = '{RowId}';
            DELETE FROM {_schema}.people WHERE id = '{RowId}';
            """;
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        var logs = await QueryLogsAsync();

        Assert.Equal(3, logs.Count);
        Assert.Equal(new[] { "INSERT", "UPDATE", "DELETE" }, logs.Select(l => l.Action).ToArray());
        Assert.All(logs, l =>
        {
            Assert.Equal("USER", l.ActorType);
            Assert.Equal(ActorUserId.ToString(), l.UserId);
            Assert.Equal("corr-test-1", l.CorrelationId);
            Assert.Equal(RowId, l.RecordId);
            Assert.Equal(_schema, l.SchemaName);
            Assert.Equal("people", l.TableName);
            Assert.False(l.OldData?.Contains("password_hash") ?? false);
            Assert.False(l.NewData?.Contains("password_hash") ?? false);
        });

        var update = logs.Single(l => l.Action == "UPDATE");
        var changed = System.Text.Json.JsonDocument.Parse(update.ChangedData!).RootElement;
        Assert.Equal("Ana P.", changed.GetProperty("name").GetString());
        Assert.Equal("ana.p@x.com", changed.GetProperty("email").GetString());
        Assert.False(changed.TryGetProperty("password_hash", out _));
        Assert.False(changed.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Rollback_NoPersisteAuditoria()
    {
        if (IsSkipped) { return; }

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {_schema}.people VALUES ('{RowId}', 'Ana', 'ana@x.com', 'secret-1');";
            await command.ExecuteNonQueryAsync();
        }
        await transaction.RollbackAsync();

        var logs = await QueryLogsAsync();
        Assert.Empty(logs);
    }

    [Fact]
    public async Task ConexionReutilizada_NoFiltraActorDeTransaccionAnterior()
    {
        if (IsSkipped) { return; }

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var tx1 = await connection.BeginTransactionAsync())
        {
            await using var cmd1 = connection.CreateCommand();
            cmd1.Transaction = tx1;
            cmd1.CommandText = """
                SELECT set_config('audit.actor_type', 'USER', true);
                SELECT set_config('audit.user_id', @userId, true);
                """;
            cmd1.Parameters.Add(new NpgsqlParameter("userId", ActorUserId.ToString()));
            await cmd1.ExecuteNonQueryAsync();

            cmd1.CommandText = $"INSERT INTO {_schema}.people VALUES ('{RowId}', 'Ana', 'ana@x.com', 'secret-1');";
            await cmd1.ExecuteNonQueryAsync();
            await tx1.CommitAsync();
        }

        await using (var tx2 = await connection.BeginTransactionAsync())
        {
            await using var cmd2 = connection.CreateCommand();
            cmd2.Transaction = tx2;
            cmd2.CommandText = $"UPDATE {_schema}.people SET email = 'nuevo@x.com' WHERE id = '{RowId}';";
            await cmd2.ExecuteNonQueryAsync();
            await tx2.CommitAsync();
        }

        var logs = await QueryLogsAsync();
        var second = logs.OrderBy(l => l.OccurredAt).Last();
        Assert.Equal("UPDATE", second.Action);
        Assert.Equal("SYSTEM", second.ActorType);
        Assert.Null(second.UserId);
    }

    [Fact]
    public async Task InterceptorEf_PropagaActorAlTrigger()
    {
        if (IsSkipped) { return; }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditTriggerInterceptor(new StubAuditActorContext()))
            .Options;

        await using var db = new AppDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {_schema}.people VALUES ('{RowId}', 'Ana', 'ana@x.com', 'secret-1');");

        await transaction.CommitAsync();

        var logs = await QueryLogsAsync();
        var log = logs.Single();
        Assert.Equal("USER", log.ActorType);
        Assert.Equal(ActorUserId.ToString(), log.UserId);
        Assert.Equal("test-role", log.UserRole);
        Assert.Equal("10.0.0.1", log.IpAddress);
        Assert.Equal("req-123", log.RequestId);
        Assert.Equal("corr-ef-1", log.CorrelationId);
    }

    private bool IsSkipped => _skipped;

    private Task ExecuteAsync(Func<NpgsqlCommand, CancellationToken, Task> action)
        => ExecuteAsync(action, CancellationToken.None);

    private async Task ExecuteAsync(Func<NpgsqlCommand, CancellationToken, Task> action, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        await action(command, ct);
    }

    private async Task<List<AuditLogRow>> QueryLogsAsync()
    {
        var result = new List<AuditLogRow>();
        await ExecuteAsync(async (cmd, ct) =>
        {
            cmd.CommandText = $"""
                SELECT action, actor_type, user_id, correlation_id, record_id, schema_name, table_name,
                       occurred_at, old_data::text, new_data::text, changed_data::text,
                       user_role, ip_address, request_id
                  FROM audit.activity_logs
                 WHERE schema_name = '{_schema}'
                 ORDER BY occurred_at;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new AuditLogRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetGuid(2).ToString(),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetDateTime(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetString(13)));
            }
        });
        return result;
    }

    private sealed record AuditLogRow(
        string Action,
        string ActorType,
        string? UserId,
        string? CorrelationId,
        string RecordId,
        string SchemaName,
        string TableName,
        DateTime OccurredAt,
        string? OldData,
        string? NewData,
        string? ChangedData,
        string? UserRole,
        string? IpAddress,
        string? RequestId);

    private sealed class StubAuditActorContext : IAuditActorContext
    {
        public AuditActorType ActorType => AuditActorType.User;
        public Guid? UserId => ActorUserId;
        public string? UserEmail => "ana@x.com";
        public string? UserRole => "test-role";
        public string? IpAddress => "10.0.0.1";
        public string? RequestId => "req-123";
        public string? CorrelationId => "corr-ef-1";
    }
}
