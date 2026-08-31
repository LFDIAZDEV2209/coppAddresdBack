using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de ítems de baterías de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestBatteryItemConfiguration
    : IEntityTypeConfiguration<HealthTestBatteryItem>
{
    public void Configure(EntityTypeBuilder<HealthTestBatteryItem> builder)
    {
        builder.ToTable("health_test_battery_items", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.BatteryId).HasColumnName("battery_id");

        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");

        builder.Property(x => x.VersionId).HasColumnName("version_id");

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.Property(x => x.IsRequired).HasColumnName("is_required").HasDefaultValue(true);

        builder.Property(x => x.FrequencyDays).HasColumnName("frequency_days");

        // Indexes
        builder
            .HasIndex(x => x.BatteryId)
            .HasDatabaseName("ix_health_test_battery_items_battery_id");

        builder
            .HasIndex(x => x.InstrumentId)
            .HasDatabaseName("ix_health_test_battery_items_instrument_id");

        builder
            .HasIndex(x => new { x.BatteryId, x.InstrumentId })
            .HasDatabaseName("ix_health_test_battery_items_battery_instrument");

        // Relationships
        builder
            .HasOne(x => x.Battery)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Version)
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
