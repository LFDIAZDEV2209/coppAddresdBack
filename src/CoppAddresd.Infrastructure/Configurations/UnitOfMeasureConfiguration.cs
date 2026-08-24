using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del catálogo de unidades de medida (schema <c>app</c>).
/// </summary>
public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("unit_of_measures", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(x => x.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_unit_of_measures_code")
            .IsUnique();
    }
}