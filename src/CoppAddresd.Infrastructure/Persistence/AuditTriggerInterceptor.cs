using System.Data.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// Propaga el contexto del actor hacia el activity log de PostgreSQL usando variables
/// GUC <b>transaccionales</b> (<c>set_config(..., true)</c>): se ejecutan justo después
/// de <c>BEGIN</c> y mueren con el commit/rollback, por lo que el connection pooling
/// jamás filtra el actor de una request hacia otra (no hay leak User A → User B).
/// El trigger <c>audit.audit_trigger_function</c> las lee vía current_setting.
/// </summary>
public sealed class AuditTriggerInterceptor(IAuditActorContext actorContext) : DbTransactionInterceptor
{
    private const string Sql = """
        SELECT set_config('audit.actor_type', @p1, true)
             , set_config('audit.user_id', @p2, true)
             , set_config('audit.user_email', @p3, true)
             , set_config('audit.user_role', @p4, true)
             , set_config('audit.ip_address', @p5, true)
             , set_config('audit.request_id', @p6, true)
             , set_config('audit.correlation_id', @p7, true)
        """;

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction transaction)
    {
        SetActorContext(connection, CancellationToken.None);
        return base.TransactionStarted(connection, eventData, transaction);
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await SetActorContextAsync(connection, cancellationToken);
        return await base.TransactionStartedAsync(connection, eventData, transaction, cancellationToken);
    }

    private void SetActorContext(DbConnection connection, CancellationToken ct)
    {
        if (connection is not NpgsqlConnection npgsql)
        {
            return;
        }

        using var command = npgsql.CreateCommand();
        command.CommandText = Sql;
        AddParameters(command);
        command.ExecuteNonQuery();
    }

    private async Task SetActorContextAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection is not NpgsqlConnection npgsql)
        {
            return;
        }

        await using var command = npgsql.CreateCommand();
        command.CommandText = Sql;
        AddParameters(command);
        await command.ExecuteNonQueryAsync(ct);
    }

    private void AddParameters(DbCommand command)
    {
        var values = new object?[]
        {
            actorContext.ActorType.ToString().ToUpperInvariant(),
            actorContext.UserId?.ToString(),
            actorContext.UserEmail,
            actorContext.UserRole,
            actorContext.IpAddress,
            actorContext.RequestId,
            actorContext.CorrelationId,
        };

        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i + 1}";
            parameter.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
