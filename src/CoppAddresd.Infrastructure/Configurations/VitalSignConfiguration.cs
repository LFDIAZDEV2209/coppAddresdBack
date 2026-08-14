using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de signos vitales en el schema <c>app</c>.</summary>
public sealed class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.ToTable("vital_signs", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.MeasuredAt)
            .HasColumnName("measured_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.Systolic)
            .HasColumnName("systolic");

        builder.Property(x => x.Diastolic)
            .HasColumnName("diastolic");

        builder.Property(x => x.HeartRate)
            .HasColumnName("heart_rate");

        builder.Property(x => x.TemperatureC)
            .HasColumnName("temperature_c")
            .HasPrecision(4, 1);

        builder.Property(x => x.O2Saturation)
            .HasColumnName("o2_saturation");

        builder.Property(x => x.HeightCm)
            .HasColumnName("height_cm")
            .HasPrecision(6, 1);

        builder.Property(x => x.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(6, 1);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.PatientId, x.MeasuredAt })
            .HasDatabaseName("ix_vital_signs_patient_measured_at");
    }
}