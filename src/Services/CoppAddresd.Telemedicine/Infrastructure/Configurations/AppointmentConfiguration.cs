using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF del agregado cita + su historial (cancelaciones y
/// reprogramaciones). El índice único parcial
/// <c>ix_appointments_professional_start_active</c> es la primera línea de
/// defensa contra la doble reserva: solo impide coincidencias entre citas en
/// estados activos (Confirmed/InProgress/Requested).
/// </summary>
public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public static readonly string ActiveStatuses =
        "appointments.status IN ('Requested','Confirmed','InProgress')";

    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.CancellationReason).HasMaxLength(2000);
        builder.Property(x => x.NoShowReason).HasMaxLength(2000);
        builder.Property(x => x.CancelledBy).HasConversion<string?>().HasMaxLength(32);

        // Concurrencia optimista: xmin nativo de PostgreSQL.
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.ProfessionalId);
        builder.HasIndex(x => x.SpecialtyId);
        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => x.ScheduledStart);
        // Nombre explícito distinto del convencional ix_appointments_request_id:
        // ese nombre ya lo ocupa el índice único parcial creado por SQL crudo en
        // AddAppointmentOverlapExclusion (anti doble confirmación). Un RenameIndex
        // automático hacia el nombre convencional chocaría con él.
        builder.HasIndex(x => x.RequestId).HasDatabaseName("ix_appointments_request_id_lookup");

        // Anti doble reserva: un profesional no puede tener dos citas activas
        // que empiecen en el mismo instante. La verificación transaccional
        // complementa este constraint (la app emite el error de negocio).
        builder.HasIndex(x => new { x.ProfessionalId, x.ScheduledStart })
            .HasDatabaseName("ix_appointments_professional_start_active")
            .HasFilter(ActiveStatuses)
            .IsUnique();        builder.HasOne(x => x.Request)
            .WithMany(r => r.Appointments)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Room)
            .WithOne(r => r.Appointment)
            .HasForeignKey<VirtualRoom>(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Encounter)
            .WithOne(e => e.Appointment)
            .HasForeignKey<ClinicalEncounter>(e => e.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Sessions)
            .WithOne(s => s.Appointment)
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
