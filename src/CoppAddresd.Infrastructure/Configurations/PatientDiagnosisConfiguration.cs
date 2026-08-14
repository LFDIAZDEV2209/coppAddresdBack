using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de diagnósticos de paciente en el schema <c>app</c>.</summary>
public sealed class PatientDiagnosisConfiguration : IEntityTypeConfiguration<PatientDiagnosis>
{
    public void Configure(EntityTypeBuilder<PatientDiagnosis> builder)
    {
        builder.ToTable("patient_diagnoses", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.Icd10CodeId)
            .HasColumnName("icd10_code_id");

        builder.Property(x => x.IsPrimary)
            .HasColumnName("is_primary");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Icd10Code)
            .WithMany(c => c.Diagnoses)
            .HasForeignKey(x => x.Icd10CodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_patient_diagnoses_patient_id");

        builder.HasIndex(x => x.Icd10CodeId)
            .HasDatabaseName("ix_patient_diagnoses_icd10_code_id");
    }
}