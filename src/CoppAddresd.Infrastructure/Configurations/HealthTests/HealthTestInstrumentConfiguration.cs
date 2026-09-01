using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de instrumentos de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestInstrumentConfiguration
    : IEntityTypeConfiguration<HealthTestInstrument>
{
    public void Configure(EntityTypeBuilder<HealthTestInstrument> builder)
    {
        builder.ToTable("health_test_instruments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(x => x.Description).HasColumnName("description");

        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

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
            .HasDatabaseName("ix_health_test_instruments_code");

        builder.HasIndex(x => x.Category).HasDatabaseName("ix_health_test_instruments_category");

        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_health_test_instruments_is_active");
    }
}
