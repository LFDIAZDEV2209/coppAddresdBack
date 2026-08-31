using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración del log de notificaciones gamificadas del programa en el
/// schema <c>app</c> (SPEC §20, A): una fila por notificación generada en los
/// flujos de otorgamiento. Índice de lectura del centro de notificaciones
/// <c>(patient_id, sent_at DESC)</c>; el anti-spam cuenta por
/// <c>(patient_id, type, día local)</c> sobre el mismo índice.
/// </summary>
public sealed class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("notifications", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(60);

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(120);

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasColumnType("text");

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasMaxLength(20)
            .HasDefaultValue("normal");

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(20)
            .HasDefaultValue("push");

        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.ReadAt)
            .HasColumnName("read_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => new { x.PatientId, x.SentAt })
            .HasDatabaseName("ix_notifications_patient_sent_at")
            .IsDescending(false, true);

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}