using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.HealthTests;

/// <summary>
/// Rollup snapshot geográfico del dashboard de Tests de Salud. PK por ciudad;
/// índice por código de estado para lecturas agrupadas por estado. Columnas
/// snake_case (convención del módulo, a diferencia de health_test_daily_metrics).
/// </summary>
public sealed class HealthTestGeoRollupConfiguration
    : IEntityTypeConfiguration<HealthTestGeoRollup>
{
    public void Configure(EntityTypeBuilder<HealthTestGeoRollup> builder)
    {
        builder.ToTable("health_test_geo_rollups", "app");

        builder.HasKey(x => x.CityId);
        builder.Property(x => x.CityId).HasColumnName("city_id");

        builder.Property(x => x.StateCode).HasColumnName("state_code").HasMaxLength(10);
        builder.Property(x => x.CityName).HasColumnName("city_name").HasMaxLength(100);
        builder.Property(x => x.PatientsCount).HasColumnName("patients_count");
        builder.Property(x => x.EvaluatedCount).HasColumnName("evaluated_count");
        builder.Property(x => x.HighRiskCount).HasColumnName("high_risk_count");
        builder.Property(x => x.ActiveAlertsCount).HasColumnName("active_alerts_count");
        builder.Property(x => x.AvgScoreSum).HasColumnName("avg_score_sum").HasColumnType("numeric(14,4)");
        builder.Property(x => x.AvgScoreCount).HasColumnName("avg_score_count");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder
            .HasIndex(x => x.StateCode)
            .HasDatabaseName("ix_health_test_geo_rollups_state_code");
    }
}
