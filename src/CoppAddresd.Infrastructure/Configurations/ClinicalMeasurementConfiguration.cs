using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de las mediciones clínicas (schema <c>app</c>).
/// El encounter es nullable (SetNull): null = monitoreo autónomo sin visita.
/// ADR-002.
/// </summary>
public sealed class ClinicalMeasurementConfiguration : IEntityTypeConfiguration<ClinicalMeasurement>
{
    public void Configure(EntityTypeBuilder<ClinicalMeasurement> builder)
    {
        builder.ToTable("clinical_measurements", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        builder.Property(x => x.EncounterId)
            .HasColumnName("encounter_id");

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .HasPrecision(12, 2);

        builder.Property(x => x.UnitId)
            .HasColumnName("unit_id");

        builder.Property(x => x.ObservedAt)
            .HasColumnName("observed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.RecordedAt)
            .HasColumnName("recorded_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(20);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_clinical_measurements_patient_id");

        builder.HasIndex(x => x.MetricId)
            .HasDatabaseName("ix_clinical_measurements_metric_id");

        builder.HasIndex(x => x.EncounterId)
            .HasDatabaseName("ix_clinical_measurements_encounter_id");

        // Serie temporal por paciente: soporta "último valor por métrica" y
        // tendencias ordenadas por fecha de observación (descendente).
        builder.HasIndex(x => new { x.PatientId, x.ObservedAt })
            .HasDatabaseName("ix_clinical_measurements_patient_observed")
            .IsDescending(false, true);

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Metric)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Encounter)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.EncounterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UnitId)
            .HasDatabaseName("ix_clinical_measurements_unit_id");
    }
}