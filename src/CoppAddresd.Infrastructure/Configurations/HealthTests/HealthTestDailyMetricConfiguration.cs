using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.HealthTests;

public sealed class HealthTestDailyMetricConfiguration : IEntityTypeConfiguration<HealthTestDailyMetric>
{
    public void Configure(EntityTypeBuilder<HealthTestDailyMetric> builder)
    {
        builder.ToTable("health_test_daily_metrics", "app");

        builder.HasKey(e => new { e.MetricDate, e.ClinicId, e.MetricKey, e.DimensionKey });

        builder.Property(e => e.MetricKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DimensionKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TotalCount).IsRequired();
        builder.Property(e => e.LastUpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.ClinicId, e.MetricKey, e.MetricDate });
    }
}
