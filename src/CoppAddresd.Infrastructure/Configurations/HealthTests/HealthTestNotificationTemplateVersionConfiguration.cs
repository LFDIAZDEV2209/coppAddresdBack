using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del historial de versiones de plantillas de notificación en el
/// schema <c>app</c> (SPEC A13).
/// </summary>
public sealed class HealthTestNotificationTemplateVersionConfiguration
    : IEntityTypeConfiguration<HealthTestNotificationTemplateVersion>
{
    public void Configure(EntityTypeBuilder<HealthTestNotificationTemplateVersion> builder)
    {
        builder.ToTable("health_test_notification_template_versions", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.TemplateId).HasColumnName("template_id");

        builder.Property(x => x.Version).HasColumnName("version");

        builder.Property(x => x.NameEs).HasColumnName("name_es").HasMaxLength(200);

        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(200);

        builder
            .Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(20)
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

        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(300);

        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Relationships
        builder
            .HasOne(x => x.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder
            .HasIndex(x => new { x.TemplateId, x.Version })
            .IsUnique()
            .HasDatabaseName("ix_health_test_notification_template_versions_template_version");
    }
}
