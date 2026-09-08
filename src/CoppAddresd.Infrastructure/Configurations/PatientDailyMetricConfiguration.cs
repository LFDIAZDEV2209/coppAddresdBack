using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class PatientDailyMetricConfiguration : IEntityTypeConfiguration<PatientDailyMetric>
{
    public void Configure(EntityTypeBuilder<PatientDailyMetric> builder)
    {
        builder.ToTable("patient_daily_metrics", "app");

        builder.HasKey(e => new { e.MetricDate, e.ClinicId, e.MetricKey, e.DimensionKey });

        builder.Property(e => e.MetricKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DimensionKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TotalCount).IsRequired();
        builder.Property(e => e.LastUpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.ClinicId, e.MetricKey, e.MetricDate });
    }
}
