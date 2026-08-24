using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del encounter canónico de mediciones clínicas (schema <c>app</c>).
/// El profesional referencia <c>erp.professionals</c> (FK cross-service).
/// ADR-001.
/// </summary>
public sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("encounters", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("planned");

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.EndedAt)
            .HasColumnName("ended_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasColumnType("text");

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_encounters_patient_id");

        builder.HasIndex(x => x.ProfessionalId)
            .HasDatabaseName("ix_encounters_professional_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_encounters_status");

        builder.HasIndex(x => x.Type)
            .HasDatabaseName("ix_encounters_type");

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Professional)
            .WithMany()
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}