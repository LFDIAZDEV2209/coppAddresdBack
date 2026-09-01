using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de especialidades en el schema <c>erp</c>.</summary>
public sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("specialties", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Code).HasDatabaseName("ix_specialties_code").IsUnique();

        // Consulta frecuente: especialidades agrupadas por categoría.
        builder
            .HasIndex(x => new { x.Category, x.SortOrder })
            .HasDatabaseName("ix_specialties_category_sort");
    }
}
