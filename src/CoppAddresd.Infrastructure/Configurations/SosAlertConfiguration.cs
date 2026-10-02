using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de <c>app.sos_alerts</c> (change sos-panic-real). La
/// integridad de la activación vive en PostgreSQL:
/// <list type="bullet">
/// <item>Índice único <c>(patient_id, idempotency_key)</c>: semántica de
/// <c>Idempotency-Key</c>; una violación <c>23505</c> sobre este índice
/// significa "reintento" y se resuelve como replay/409.</item>
/// <item>Índice único parcial sobre <c>patient_id</c> donde
/// <c>status = 'Activa'</c>: una sola alerta activa por paciente (la
/// violación significa "ya hay una activa" → 409 con la existente).</item>
/// <item>CHECK constraints de rango geográfico: defensa en profundidad de la
/// validación de coordenadas (REQ-SOS-01).</item>
/// </list>
/// Sin FK a auth.users (los actores son referencias por Id, misma convención
/// que las tablas del schema app con actores de autenticación).
/// </summary>
public sealed class SosAlertConfiguration : IEntityTypeConfiguration<SosAlert>
{
    public void Configure(EntityTypeBuilder<SosAlert> builder)
    {
        builder.ToTable("sos_alerts", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(36);

        builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64);

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.Latitude).HasColumnName("latitude");

        builder.Property(x => x.Longitude).HasColumnName("longitude");

        builder.Property(x => x.AccuracyMeters).HasColumnName("accuracy_meters");

        builder
            .Property(x => x.LocationCapturedAt)
            .HasColumnName("location_captured_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.HeartRate).HasColumnName("heart_rate");

        builder.Property(x => x.Spo2).HasColumnName("spo2");

        builder.Property(x => x.BloodPressure).HasColumnName("blood_pressure").HasMaxLength(20);

        builder
            .Property(x => x.DestinationPhoneE164)
            .HasColumnName("destination_phone_e164")
            .HasMaxLength(20);

        builder
            .Property(x => x.SmsChannelStatus)
            .HasColumnName("sms_channel_status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder
            .Property(x => x.SmsUpdatedAt)
            .HasColumnName("sms_updated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.SmsDetail).HasColumnName("sms_detail").HasMaxLength(100);

        builder
            .Property(x => x.VoiceChannelStatus)
            .HasColumnName("voice_channel_status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder
            .Property(x => x.VoiceUpdatedAt)
            .HasColumnName("voice_updated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.VoiceDetail).HasColumnName("voice_detail").HasMaxLength(100);

        builder
            .Property(x => x.PushChannelStatus)
            .HasColumnName("push_channel_status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder
            .Property(x => x.PushUpdatedAt)
            .HasColumnName("push_updated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.PushRecipients).HasColumnName("push_recipients");

        builder.Property(x => x.PushDetail).HasColumnName("push_detail").HasMaxLength(200);

        builder.Property(x => x.AttendedBy).HasColumnName("attended_by");

        builder
            .Property(x => x.AttendedAt)
            .HasColumnName("attended_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        builder
            .Property(x => x.CancelledAt)
            .HasColumnName("cancelled_at")
            .HasColumnType("timestamptz");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Índice de activación idempotente (REQ-SOS-01): (patient_id, idempotency_key).
        builder
            .HasIndex(x => new { x.PatientId, x.IdempotencyKey })
            .HasDatabaseName("uq_sos_alerts_patient_idempotency")
            .IsUnique();

        // Índice único parcial: una sola alerta activa por paciente. El filter
        // usa el valor string del enum (HasConversion<string>) y coincide con
        // el patrón del índice parcial de program_enrollments.
        builder
            .HasIndex(x => x.PatientId)
            .HasDatabaseName("uq_sos_alerts_patient_active")
            .IsUnique()
            .HasFilter("\"status\" = 'Activa'");

        // Índice de consulta de staff (dashboard futuro): alertas recientes por estado.
        builder
            .HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_sos_alerts_status_created_at");

        // Defensa en profundidad del rango geográfico (REQ-SOS-01/D6).
        builder.ToTable(
            "sos_alerts",
            "app",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_sos_alerts_latitude_range",
                    "\"latitude\" IS NULL OR (\"latitude\" >= -90.0 AND \"latitude\" <= 90.0)"
                );
                t.HasCheckConstraint(
                    "ck_sos_alerts_longitude_range",
                    "\"longitude\" IS NULL OR (\"longitude\" >= -180.0 AND \"longitude\" <= 180.0)"
                );
            }
        );

        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
