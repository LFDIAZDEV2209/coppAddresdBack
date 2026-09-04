using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

public sealed class ProgramDailyMetricConfiguration : IEntityTypeConfiguration<ProgramDailyMetric>
{
    public void Configure(EntityTypeBuilder<ProgramDailyMetric> builder)
    {
        builder.ToTable("program_daily_metrics", "app");

        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate)
            .HasColumnName("metric_date")
            .HasColumnType("date");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id");

        builder.Property(x => x.MetricKey)
            .HasColumnName("metric_key")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.DimensionKey)
            .HasColumnName("dimension_key")
            .HasMaxLength(60)
            .HasDefaultValue("general")
            .IsRequired();

        builder.Property(x => x.TotalCount)
            .HasColumnName("total_count")
            .HasDefaultValue(0L);

        builder.Property(x => x.TotalValue)
            .HasColumnName("total_value")
            .HasColumnType("numeric(14,2)")
            .HasDefaultValue(0.00m);

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.MetricDate, x.MetricKey })
            .HasDatabaseName("ix_program_daily_metrics_lookup");
    }
}
