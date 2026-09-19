using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF de la pre-consulta del paciente (F4). Una fila por cita
/// (índice único en <c>appointment_id</c>, refuerza el upsert idempotente); la
/// FK a la cita es intra-schema y cascadea con ella. Longitudes alineadas con
/// el contrato de la API (500/4000/2000/2000).
/// </summary>
public sealed class PreVisitIntakeConfiguration : IEntityTypeConfiguration<PreVisitIntake>
{
    public void Configure(EntityTypeBuilder<PreVisitIntake> builder)
    {
        builder.ToTable("pre_visit_intakes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Symptoms).HasMaxLength(4000);
        builder.Property(x => x.Allergies).HasMaxLength(2000);
        builder.Property(x => x.Medications).HasMaxLength(2000);

        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => x.PatientId);

        builder.HasOne(x => x.Appointment)
            .WithOne(a => a.PreVisitIntake)
            .HasForeignKey<PreVisitIntake>(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Configuración EF de la adenda del encuentro (F4). Tabla append-only: sin
/// <c>updated_at</c> ni endpoints de mutación; el índice
/// <c>(encounter_id, created_at)</c> cubre el listado cronológico.
/// </summary>
public sealed class EncounterAddendumConfiguration : IEntityTypeConfiguration<EncounterAddendum>
{
    public void Configure(EntityTypeBuilder<EncounterAddendum> builder)
    {
        builder.ToTable("encounter_addenda");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuthorName).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => new { x.EncounterId, x.CreatedAt });

        builder.HasOne(x => x.Encounter)
            .WithMany(e => e.Addenda)
            .HasForeignKey(x => x.EncounterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
