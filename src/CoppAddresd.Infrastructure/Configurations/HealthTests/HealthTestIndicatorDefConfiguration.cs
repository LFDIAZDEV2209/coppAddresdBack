using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de definiciones de indicadores en el schema <c>app</c>.</summary>
public sealed class HealthTestIndicatorDefConfiguration
    : IEntityTypeConfiguration<HealthTestIndicatorDef>
{
    public void Configure(EntityTypeBuilder<HealthTestIndicatorDef> builder)
    {
        builder.ToTable("health_test_indicator_defs", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(x => x.Description).HasColumnName("description");

        builder.Property(x => x.Computation).HasColumnName("computation").HasColumnType("jsonb");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder
            .HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ix_health_test_indicator_defs_code");

        builder
            .HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_health_test_indicator_defs_is_active");
    }
}
