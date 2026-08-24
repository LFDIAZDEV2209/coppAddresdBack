using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>Configuración EF del encuentro clínico.</summary>
public sealed class EncounterConfiguration : IEntityTypeConfiguration<ClinicalEncounter>
{
    public void Configure(EntityTypeBuilder<ClinicalEncounter> builder)
    {
        builder.ToTable("clinical_encounters");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ClinicalData).HasColumnType("jsonb");
        builder.Property(x => x.Notes).HasMaxLength(4000);

        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.ProfessionalId);
        builder.HasIndex(x => x.EncounterDate);

        // EncounterId → encounter_id (uuid nullable, convención snake_case).
        // La FK hacia app.encounters (encounter canónico del core) se crea por
        // SQL en la migración, fuera del modelo EF (igual que los FKs externos
        // hacia auth.users): este servicio es standalone y no conoce el tipo.
        builder.HasIndex(x => x.EncounterId);
    }
}

/// <summary>Configuración EF del historial de cancelaciones (append-only).</summary>
public sealed class CancellationConfiguration : IEntityTypeConfiguration<AppointmentCancellation>
{
    public void Configure(EntityTypeBuilder<AppointmentCancellation> builder)
    {
        builder.ToTable("appointment_cancellations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CancelledBy).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).HasMaxLength(2000);

        builder.HasIndex(x => x.AppointmentId);

        builder.HasOne(x => x.Appointment)
            .WithMany(a => a.Cancellations)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Configuración EF del historial de reprogramaciones (append-only).</summary>
public sealed class RescheduleConfiguration : IEntityTypeConfiguration<AppointmentReschedule>
{
    public void Configure(EntityTypeBuilder<AppointmentReschedule> builder)
    {
        builder.ToTable("appointment_reschedules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestedBy).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).HasMaxLength(2000);

        builder.HasIndex(x => x.AppointmentId);

        builder.HasOne(x => x.Appointment)
            .WithMany(a => a.Reschedules)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
