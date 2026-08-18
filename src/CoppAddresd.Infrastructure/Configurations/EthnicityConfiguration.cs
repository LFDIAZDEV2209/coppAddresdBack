using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de etnias en el schema <c>app</c>.</summary>
public sealed class EthnicityConfiguration : IEntityTypeConfiguration<Ethnicity>
{
    public void Configure(EntityTypeBuilder<Ethnicity> builder)
    {
        builder.ToTable("ethnicities", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_ethnicities_code")
            .IsUnique();
    }
}