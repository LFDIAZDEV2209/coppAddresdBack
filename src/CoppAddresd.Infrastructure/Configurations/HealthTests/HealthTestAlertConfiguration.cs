using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de alertas clínicas de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestAlertConfiguration : IEntityTypeConfiguration<HealthTestAlert>
{
    public void Configure(EntityTypeBuilder<HealthTestAlert> builder)
    {
        builder.ToTable("health_test_alerts", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.ResultId).HasColumnName("result_id");

        builder.Property(x => x.RuleId).HasColumnName("rule_id");

        builder
            .Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestSeverity.high)
            .HasConversion<string>();

        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);

        builder.Property(x => x.Body).HasColumnName("body");

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestAlertStatus.active)
            .HasConversion<string>();

        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");

        builder
            .Property(x => x.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");

        builder
            .Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at")
            .HasColumnType("timestamptz");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_health_test_alerts_patient_id");

        builder
            .HasIndex(x => new { x.Status, x.Severity })
            .HasDatabaseName("ix_health_test_alerts_status_severity");

        builder
            .HasIndex(x => new { x.ResultId, x.RuleId })
            .HasDatabaseName("ix_health_test_alerts_result_rule");

        // Relationships
        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Result)
            .WithMany()
            .HasForeignKey(x => x.ResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Rule)
            .WithMany()
            .HasForeignKey(x => x.RuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
