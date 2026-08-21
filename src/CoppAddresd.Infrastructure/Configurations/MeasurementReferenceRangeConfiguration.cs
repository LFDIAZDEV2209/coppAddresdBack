using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de los rangos de referencia de mediciones clínicas
/// (schema <c>app</c>).
/// </summary>
public sealed class MeasurementReferenceRangeConfiguration : IEntityTypeConfiguration<MeasurementReferenceRange>
{
    public void Configure(EntityTypeBuilder<MeasurementReferenceRange> builder)
    {
        builder.ToTable("measurement_reference_ranges", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        builder.Property(x => x.AgeMin)
            .HasColumnName("age_min");

        builder.Property(x => x.AgeMax)
            .HasColumnName("age_max");

        builder.Property(x => x.Gender)
            .HasColumnName("gender")
            .HasMaxLength(1);

        builder.Property(x => x.MinValue)
            .HasColumnName("min_value")
            .HasPrecision(10, 2);

        builder.Property(x => x.MaxValue)
            .HasColumnName("max_value")
            .HasPrecision(10, 2);

        builder.Property(x => x.UnitId)
            .HasColumnName("unit_id");

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasDefaultValue(0);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasIndex(x => x.MetricId)
            .HasDatabaseName("ix_measurement_reference_ranges_metric_id");

        builder.HasOne(x => x.Metric)
            .WithMany(x => x.ReferenceRanges)
            .HasForeignKey(x => x.MetricId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.ReferenceRanges)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UnitId)
            .HasDatabaseName("ix_measurement_reference_ranges_unit_id");
    }
}