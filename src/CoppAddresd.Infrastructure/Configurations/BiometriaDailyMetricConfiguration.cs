using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class BiometriaDailyMetricConfiguration : IEntityTypeConfiguration<BiometriaDailyMetric>
{
    public void Configure(EntityTypeBuilder<BiometriaDailyMetric> builder)
    {
        builder.ToTable("biometria_daily_metrics", "app");
        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate)
            .HasColumnName("metric_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.MetricKey)
            .HasColumnName("metric_key")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DimensionKey)
            .HasColumnName("dimension_key")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TotalCount)
            .HasColumnName("total_count")
            .IsRequired();

        builder.Property(x => x.TotalValue)
            .HasColumnName("total_value")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        builder.HasIndex(x => new { x.MetricKey, x.MetricDate })
            .HasDatabaseName("ix_biometria_daily_metrics_metric_key_metric_date");
    }
}
