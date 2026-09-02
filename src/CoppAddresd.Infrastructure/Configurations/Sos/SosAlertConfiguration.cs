using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.Sos;

/// <summary>
/// Configuración EF de las alertas SOS en el schema <c>sos</c>.
/// Tabla <c>sos_alerts</c> con columnas jsonb para snapshot de vitales y
/// resultados por canal. Indexada por <c>(patient_id, triggered_at_utc DESC)</c>
/// para el historial del paciente.
/// </summary>
public sealed class SosAlertConfiguration : IEntityTypeConfiguration<SosAlert>
{
    public void Configure(EntityTypeBuilder<SosAlert> builder)
    {
        builder.ToTable("sos_alerts", "sos");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.TriggeredAtUtc)
            .HasColumnName("triggered_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("double precision");

        builder.Property(x => x.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("double precision");

        builder.Property(x => x.AccuracyMeters)
            .HasColumnName("accuracy_meters")
            .HasColumnType("double precision");

        builder.Property(x => x.LocationLabel)
            .HasColumnName("location_label")
            .HasMaxLength(500);

        builder.Property(x => x.VitalsSnapshot)
            .HasColumnName("vitals_snapshot")
            .HasColumnType("jsonb");

        builder.Property(x => x.MessageText)
            .HasColumnName("message_text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.EmergencyContactName)
            .HasColumnName("emergency_contact_name")
            .HasMaxLength(200);

        builder.Property(x => x.EmergencyContactRelationship)
            .HasColumnName("emergency_contact_relationship")
            .HasMaxLength(100);

        builder.Property(x => x.EmergencyContactPhone)
            .HasColumnName("emergency_contact_phone")
            .HasMaxLength(30);

        builder.Property(x => x.EmergencyContactEmail)
            .HasColumnName("emergency_contact_email")
            .HasMaxLength(254);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ChannelResults)
            .HasColumnName("channel_results")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => new { x.PatientId, x.TriggeredAtUtc })
            .HasDatabaseName("ix_sos_alerts_patient_triggered_at")
            .IsDescending(false, true);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_sos_alerts_status");

        // Relationships
        builder.HasOne<PatientProfile>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
