using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

public sealed class ProfessionalDailyStatConfiguration : IEntityTypeConfiguration<ProfessionalDailyStat>
{
    public void Configure(EntityTypeBuilder<ProfessionalDailyStat> builder)
    {
        builder.ToTable("professional_daily_stats");

        builder.HasKey(x => new { x.ProfessionalId, x.MetricDate });

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.MetricDate)
            .HasColumnName("metric_date")
            .HasColumnType("date");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id");

        builder.Property(x => x.TotalAppointments)
            .HasColumnName("total_appointments")
            .HasDefaultValue(0);

        builder.Property(x => x.CompletedAppointments)
            .HasColumnName("completed_appointments")
            .HasDefaultValue(0);

        builder.Property(x => x.CancelledAppointments)
            .HasColumnName("cancelled_appointments")
            .HasDefaultValue(0);

        builder.Property(x => x.NoShowAppointments)
            .HasColumnName("no_show_appointments")
            .HasDefaultValue(0);

        builder.Property(x => x.UniquePatients)
            .HasColumnName("unique_patients")
            .HasDefaultValue(0);

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.MetricDate)
            .HasDatabaseName("ix_professional_daily_stats_date");
    }
}
