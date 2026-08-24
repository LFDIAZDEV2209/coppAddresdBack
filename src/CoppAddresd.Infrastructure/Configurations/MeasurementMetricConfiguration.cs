using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del catálogo de métricas de mediciones clínicas (schema <c>app</c>).
/// </summary>
public sealed class MeasurementMetricConfiguration : IEntityTypeConfiguration<MeasurementMetric>
{
    public void Configure(EntityTypeBuilder<MeasurementMetric> builder)
    {
        builder.ToTable("measurement_metrics", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.DefaultUnitId)
            .HasColumnName("default_unit_id");

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(50);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_measurement_metrics_code")
            .IsUnique();

        builder.HasIndex(x => x.DefaultUnitId)
            .HasDatabaseName("ix_measurement_metrics_default_unit_id");

        builder.HasOne(x => x.DefaultUnit)
            .WithMany(x => x.Metrics)
            .HasForeignKey(x => x.DefaultUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}