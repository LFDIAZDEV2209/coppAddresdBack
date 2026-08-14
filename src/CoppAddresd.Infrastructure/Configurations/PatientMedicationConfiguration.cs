using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de medicamentos de paciente en el schema <c>app</c>.</summary>
public sealed class PatientMedicationConfiguration : IEntityTypeConfiguration<PatientMedication>
{
    public void Configure(EntityTypeBuilder<PatientMedication> builder)
    {
        builder.ToTable("patient_medications", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.MedicationId)
            .HasColumnName("medication_id");

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(100);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Medication)
            .WithMany(m => m.PatientMedications)
            .HasForeignKey(x => x.MedicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_patient_medications_patient_id");

        builder.HasIndex(x => x.MedicationId)
            .HasDatabaseName("ix_patient_medications_medication_id");
    }
}