using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

public sealed class AppointmentDailyMetricConfiguration : IEntityTypeConfiguration<AppointmentDailyMetric>
{
    public void Configure(EntityTypeBuilder<AppointmentDailyMetric> builder)
    {
        builder.ToTable("appointment_daily_metrics");

        builder.HasKey(x => new { x.MetricDate, x.ProfessionalId, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate)
            .HasColumnName("metric_date")
            .HasColumnType("date");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id")
            .HasDefaultValue(Guid.Empty);

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

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.MetricDate, x.ProfessionalId, x.MetricKey })
            .HasDatabaseName("ix_appointment_daily_metrics_lookup");
    }
}
