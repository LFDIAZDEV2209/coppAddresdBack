using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Prueba end-to-end del mecanismo de auditoría: EF Core → Npgsql → PostgreSQL
/// → trigger → audit.activity_logs. El código de prueba NUNCA toca ActivityLog:
/// solo escribe en la tabla de negocio y verifica que la auditoría aparece sola.
/// Requiere COP_TEST_DB_CONNECTION (si falta, se omite).
/// </summary>
public sealed class ActivityLogE2ETests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";
    private const string Email = "e2e@example.com";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private readonly string _schema = $"e2e_{Guid.NewGuid():N}";
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
                CREATE TABLE {_schema}.customers (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    name text,
                    email text NOT NULL,
                    status varchar(20) NOT NULL DEFAULT 'ACTIVE',
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    secret_value text);
                SELECT audit.attach_table_audit('{_schema}', 'customers', 'id', 'secret_value');
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

    private AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditTriggerInterceptor(new StubAuditActorContext()))
            .Options);

    [Fact]
    public async Task CrudCompleto_GeneraAuditoriaAutomaticaSinDuplicacion()
    {
        if (_skipped)
        {
            return;
        }

        await using var db = CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync($"""
            INSERT INTO {_schema}.customers (name, email, status, secret_value)
            VALUES ('Carlos Test', '{Email}', 'ACTIVE', 'SUPER_SECRET_VALUE');
            UPDATE {_schema}.customers SET name = 'Carlos Updated', status = 'INACTIVE' WHERE email = '{Email}';
            DELETE FROM {_schema}.customers WHERE email = '{Email}';
            """);

        await transaction.CommitAsync();

        var logs = await QueryLogsAsync();

        // No duplicación: exactamente 1 log por operación
        Assert.Equal(3, logs.Count);
        Assert.Equal(new[] { "INSERT", "UPDATE", "DELETE" }, logs.Select(l => l.Action).ToArray());

        // El secreto NUNCA aparece en ningún payload
        Assert.All(logs, l =>
        {
            Assert.False(l.NewData?.Contains("secret_value") ?? false);
            Assert.False(l.OldData?.Contains("secret_value") ?? false);
        });

        // changed_data solo con las columnas realmente cambiadas
        var update = logs.Single(l => l.Action == "UPDATE");
        var changed = System.Text.Json.JsonDocument.Parse(update.ChangedData!).RootElement;
        var changedKeys = changed.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Contains("name", changedKeys);
        Assert.Contains("status", changedKeys);
        Assert.DoesNotContain("email", changedKeys);
        Assert.DoesNotContain("created_at", changedKeys);

        // old/new coherentes
        var insert = logs.Single(l => l.Action == "INSERT");
        var delete = logs.Single(l => l.Action == "DELETE");
        Assert.Null(insert.OldData);
        Assert.Null(insert.ChangedData);
        Assert.NotNull(insert.NewData);
        Assert.NotNull(delete.OldData);
        Assert.Null(delete.NewData);
        Assert.NotEmpty(insert.RecordId);
        Assert.Equal(insert.RecordId, update.RecordId);
        Assert.Equal(insert.RecordId, delete.RecordId);
    }

    [Fact]
    public async Task CambiosNull_SeDetectanSoloCuandoCambian()
    {
        if (_skipped)
        {
            return;
        }

        await using var db = CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync($"""
            INSERT INTO {_schema}.customers (name, email) VALUES (NULL, '{Email}');
            UPDATE {_schema}.customers SET name = 'Con Valor' WHERE email = '{Email}';
            UPDATE {_schema}.customers SET name = NULL WHERE email = '{Email}';
            UPDATE {_schema}.customers SET name = NULL WHERE email = '{Email}';
            """);

        await transaction.CommitAsync();

        var logs = await QueryLogsAsync();
        Assert.Equal(4, logs.Count);

        var updates = logs.Where(l => l.Action == "UPDATE").Select(l => l.ChangedData).ToArray();
        Assert.Equal(3, updates.Length);

        // NULL -> valor: se registra
        var nullToValue = System.Text.Json.JsonDocument.Parse(updates[0]!).RootElement;
        Assert.Equal("Con Valor", nullToValue.GetProperty("name").GetString());

        // valor -> NULL: se registra
        var valueToNull = System.Text.Json.JsonDocument.Parse(updates[1]!).RootElement;
        Assert.Equal(System.Text.Json.JsonValueKind.Null, valueToNull.GetProperty("name").ValueKind);

        // NULL -> NULL: NO se registra como cambio
        Assert.Null(updates[2]);
    }

    [Fact]
    public async Task Rollback_NoGeneraAuditoria()
    {
        if (_skipped)
        {
            return;
        }

        await using var db = CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {_schema}.customers (name, email) VALUES ('Rollback', '{Email}');");
        await transaction.RollbackAsync();

        var logs = await QueryLogsAsync();
        Assert.Empty(logs);
    }

    private async Task ExecuteAsync(Func<NpgsqlCommand, CancellationToken, Task> action, CancellationToken ct = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        await action(command, ct);
    }

    private async Task<List<LogRow>> QueryLogsAsync()
    {
        var result = new List<LogRow>();
        await ExecuteAsync(async (cmd, ct) =>
        {
            cmd.CommandText = $"""
                SELECT action, record_id, old_data::text, new_data::text, changed_data::text
                  FROM audit.activity_logs
                 WHERE schema_name = '{_schema}'
                 ORDER BY occurred_at;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new LogRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
            }
        });
        return result;
    }

    private sealed record LogRow(string Action, string RecordId, string? OldData, string? NewData, string? ChangedData);

    private sealed class StubAuditActorContext : IAuditActorContext
    {
        public AuditActorType ActorType => AuditActorType.User;
        public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? UserEmail => "e2e@example.com";
        public string? UserRole => "e2e-role";
        public string? IpAddress => "10.0.0.1";
        public string? RequestId => "req-e2e";
        public string? CorrelationId => "corr-e2e";
    }
}
