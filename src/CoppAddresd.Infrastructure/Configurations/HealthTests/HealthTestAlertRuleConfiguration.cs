using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de reglas de alerta en el schema <c>app</c>.</summary>
public sealed class HealthTestAlertRuleConfiguration : IEntityTypeConfiguration<HealthTestAlertRule>
{
    public void Configure(EntityTypeBuilder<HealthTestAlertRule> builder)
    {
        builder.ToTable("health_test_alert_rules", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(x => x.Condition).HasColumnName("condition").HasColumnType("jsonb");

        builder
            .Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestSeverity.high)
            .HasConversion<string>();

        builder.Property(x => x.MessageTemplate).HasColumnName("message_template");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder
            .HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ix_health_test_alert_rules_code");

        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_health_test_alert_rules_is_active");
    }
}
