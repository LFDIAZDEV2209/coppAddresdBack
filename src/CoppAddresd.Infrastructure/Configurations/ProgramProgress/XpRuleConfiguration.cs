using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración del catálogo de reglas de XP en el schema <c>app</c>
/// (SPEC §14): <c>code</c> único (clave de negocio del UPSERT del seeder y de
/// la FK de <c>xp_ledger.rule_code</c>), índices por categoría y estado, y
/// CHECK constraints que espejan la validación de la capa de aplicación
/// (multiplier &gt; 0, base_xp &gt;= 0, topes &gt;= 0).
/// </summary>
public sealed class XpRuleConfiguration : IEntityTypeConfiguration<XpRule>
{
    public void Configure(EntityTypeBuilder<XpRule> builder)
    {
        builder.ToTable("xp_rules", "app", t =>
        {
            t.HasCheckConstraint("ck_xp_rules_multiplier_positive", "\"multiplier\" > 0");
            t.HasCheckConstraint("ck_xp_rules_base_xp_non_negative", "\"base_xp\" IS NULL OR \"base_xp\" >= 0");
            t.HasCheckConstraint(
                "ck_xp_rules_max_limits_non_negative",
                "(\"max_per_day\" IS NULL OR \"max_per_day\" >= 0) AND (\"max_per_week\" IS NULL OR \"max_per_week\" >= 0)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(60);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(40);

        builder.Property(x => x.BaseXp)
            .HasColumnName("base_xp");

        builder.Property(x => x.Multiplier)
            .HasColumnName("multiplier")
            .HasPrecision(4, 2)
            .HasDefaultValue(1.0m);

        builder.Property(x => x.MaxPerDay)
            .HasColumnName("max_per_day");

        builder.Property(x => x.MaxPerWeek)
            .HasColumnName("max_per_week");

        builder.Property(x => x.RequiresValidation)
            .HasColumnName("requires_validation")
            .HasDefaultValue(false);

        builder.Property(x => x.Active)
            .HasColumnName("active")
            .HasDefaultValue(true);

        builder.Property(x => x.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("date")
            .HasDefaultValueSql("CURRENT_DATE");

        builder.Property(x => x.ValidUntil)
            .HasColumnName("valid_until")
            .HasColumnType("date");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Code)
            .HasDatabaseName("uq_xp_rules_code")
            .IsUnique();

        builder.HasIndex(x => x.Category)
            .HasDatabaseName("ix_xp_rules_category");

        builder.HasIndex(x => x.Active)
            .HasDatabaseName("ix_xp_rules_active");
    }
}