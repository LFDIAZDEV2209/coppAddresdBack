using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del rollup diario de Inventario &amp; Farmacia (Dashboard #6).
/// Misma convención que las demás tablas del módulo: schema <c>erp</c>.
/// </summary>
public sealed class InventoryDailyMetricConfiguration : IEntityTypeConfiguration<InventoryDailyMetric>
{
    public void Configure(EntityTypeBuilder<InventoryDailyMetric> builder)
    {
        builder.ToTable("inventory_daily_metrics", "erp");

        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate).HasColumnName("metric_date");
        builder.Property(x => x.MetricKey).HasColumnName("metric_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DimensionKey).HasColumnName("dimension_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TotalCount).HasColumnName("total_count").HasDefaultValue(0L);
        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamp without time zone");

        builder.HasIndex(x => new { x.MetricKey, x.DimensionKey, x.MetricDate })
            .HasDatabaseName("ix_inventory_daily_metrics_key_dim_date");
        builder.HasIndex(x => x.MetricDate)
            .HasDatabaseName("ix_inventory_daily_metrics_date");
    }
}