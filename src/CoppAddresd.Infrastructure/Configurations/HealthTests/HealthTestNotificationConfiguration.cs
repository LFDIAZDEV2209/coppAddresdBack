using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del registro de entregas de notificaciones por alertas en el
/// schema <c>app</c> (SPEC A13). Es un log auditable: los FKs son opcionales y
/// se anulan (SetNull) si se elimina la alerta, el paciente o la plantilla.
/// </summary>
public sealed class HealthTestNotificationConfiguration
    : IEntityTypeConfiguration<HealthTestNotification>
{
    public void Configure(EntityTypeBuilder<HealthTestNotification> builder)
    {
        builder.ToTable("health_test_notifications", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AlertId).HasColumnName("alert_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder
            .Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(20)
            .HasDefaultValue(NotificationChannel.community)
            .HasConversion<string>();

        builder
            .Property(x => x.Language)
            .HasColumnName("language")
            .HasMaxLength(5)
            .HasDefaultValue(NotificationLanguage.es)
            .HasConversion<string>();

        builder.Property(x => x.TemplateId).HasColumnName("template_id");

        builder.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(200);

        builder.Property(x => x.RenderedBody).HasColumnName("rendered_body").HasColumnType("text");

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(NotificationStatus.queued)
            .HasConversion<string>();

        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40);

        builder.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);

        builder.Property(x => x.Error).HasColumnName("error").HasColumnType("text");

        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.SentAt).HasColumnName("sent_at").HasColumnType("timestamptz");

        // Relationships
        builder
            .HasOne(x => x.Alert)
            .WithMany()
            .HasForeignKey(x => x.AlertId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.AlertId).HasDatabaseName("ix_health_test_notifications_alert_id");

        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_health_test_notifications_patient_id");

        builder
            .HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_health_test_notifications_status_created_at");

        builder.HasIndex(x => x.Channel).HasDatabaseName("ix_health_test_notifications_channel");

        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_health_test_notifications_template_id");
    }
}
