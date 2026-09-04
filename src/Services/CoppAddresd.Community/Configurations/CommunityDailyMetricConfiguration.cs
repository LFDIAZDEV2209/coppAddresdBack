using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Configurations;

public class CommunityDailyMetricConfiguration : IEntityTypeConfiguration<CommunityDailyMetric>
{
    public void Configure(EntityTypeBuilder<CommunityDailyMetric> builder)
    {
        builder.ToTable("community_daily_metrics", "community");

        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate).HasColumnName("metric_date");
        builder.Property(x => x.MetricKey).HasColumnName("metric_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DimensionKey).HasColumnName("dimension_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TotalCount).HasColumnName("total_count").HasDefaultValue(0L);
        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamp without time zone");

        builder.HasIndex(x => new { x.MetricKey, x.DimensionKey, x.MetricDate })
            .HasDatabaseName("ix_community_daily_metrics_key_dim_date");
    }
}