using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de las reglas de seguridad clínica para generación de planes
/// (schema <c>app</c>). La severidad se persiste como texto legible
/// (<c>Block</c>/<c>Warning</c>). Índice compuesto (is_active, metric_code)
/// para la evaluación en caliente y uno simple por métrica para consultas
/// por código.
/// </summary>
public sealed class PlanSafetyRuleConfiguration : IEntityTypeConfiguration<PlanSafetyRule>
{
    public void Configure(EntityTypeBuilder<PlanSafetyRule> builder)
    {
        builder.ToTable("plan_safety_rules", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MetricCode)
            .HasColumnName("metric_code")
            .HasMaxLength(50);

        builder.Property(x => x.Operator)
            .HasColumnName("operator")
            .HasMaxLength(10);

        builder.Property(x => x.ThresholdMin)
            .HasColumnName("threshold_min")
            .HasPrecision(12, 2);

        builder.Property(x => x.ThresholdMax)
            .HasColumnName("threshold_max")
            .HasPrecision(12, 2);

        builder.Property(x => x.UnitCode)
            .HasColumnName("unit_code")
            .HasMaxLength(20);

        builder.Property(x => x.Restriction)
            .HasColumnName("restriction")
            .HasColumnType("text");

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Evaluación en caliente (reglas activas por métrica) y consulta por
        // código de métrica. El compuesto cubre el filtro por IsActive solo;
        // el simple respalda joins/reportes por metric_code.
        builder.HasIndex(x => new { x.IsActive, x.MetricCode })
            .HasDatabaseName("ix_plan_safety_rules_active_metric");

        builder.HasIndex(x => x.MetricCode)
            .HasDatabaseName("ix_plan_safety_rules_metric_code");
    }
}