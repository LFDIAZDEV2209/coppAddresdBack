using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de plantillas de notificación por alertas en el schema
/// <c>app</c> (SPEC A13, Template Studio).
/// </summary>
public sealed class HealthTestNotificationTemplateConfiguration
    : IEntityTypeConfiguration<HealthTestNotificationTemplate>
{
    public void Configure(EntityTypeBuilder<HealthTestNotificationTemplate> builder)
    {
        builder.ToTable("health_test_notification_templates", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.NameEs).HasColumnName("name_es").HasMaxLength(200);

        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(200);

        builder
            .Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(20)
            .HasDefaultValue(NotificationChannel.community)
            .HasConversion<string>();

        builder
            .Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.TestCategory).HasColumnName("test_category").HasMaxLength(64);

        builder.Property(x => x.IndicatorCode).HasColumnName("indicator_code").HasMaxLength(64);

        builder.Property(x => x.SubjectEs).HasColumnName("subject_es").HasMaxLength(200);

        builder.Property(x => x.SubjectEn).HasColumnName("subject_en").HasMaxLength(200);

        builder.Property(x => x.BodyTemplateEs).HasColumnName("body_template_es").HasColumnType("text");

        builder.Property(x => x.BodyTemplateEn).HasColumnName("body_template_en").HasColumnType("text");

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
            .HasDatabaseName("ix_health_test_notification_templates_code");

        builder
            .HasIndex(x => x.Channel)
            .HasDatabaseName("ix_health_test_notification_templates_channel");

        builder
            .HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_health_test_notification_templates_is_active");
    }
}
