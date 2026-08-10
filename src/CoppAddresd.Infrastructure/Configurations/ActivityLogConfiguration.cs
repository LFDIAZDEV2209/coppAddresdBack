using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del activity log. La tabla es de solo lectura para EF Core:
/// la escribe el trigger <c>audit.audit_trigger_function</c>. Lectura con AsNoTracking.
/// </summary>
public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs", "audit");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(10)
            .HasConversion<string>();

        builder.Property(x => x.SchemaName)
            .HasColumnName("schema_name")
            .HasMaxLength(64);

        builder.Property(x => x.TableName)
            .HasColumnName("table_name")
            .HasMaxLength(64);

        builder.Property(x => x.RecordId)
            .HasColumnName("record_id");

        builder.Property(x => x.ActorType)
            .HasColumnName("actor_type")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.UserEmail)
            .HasColumnName("user_email")
            .HasMaxLength(320);

        builder.Property(x => x.UserRole)
            .HasColumnName("user_role")
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(x => x.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        builder.Property(x => x.OldData)
            .HasColumnName("old_data")
            .HasColumnType("jsonb");

        builder.Property(x => x.NewData)
            .HasColumnName("new_data")
            .HasColumnType("jsonb");

        builder.Property(x => x.ChangedData)
            .HasColumnName("changed_data")
            .HasColumnType("jsonb");

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("ix_activity_logs_occurred_at");

        builder.HasIndex(x => new { x.TableName, x.RecordId })
            .HasDatabaseName("ix_activity_logs_table_record");
    }
}
