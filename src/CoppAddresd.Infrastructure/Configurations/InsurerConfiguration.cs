using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de aseguradoras en el schema <c>app</c>.</summary>
public sealed class InsurerConfiguration : IEntityTypeConfiguration<Insurer>
{
    public void Configure(EntityTypeBuilder<Insurer> builder)
    {
        builder.ToTable("insurers", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_insurers_name")
            .IsUnique();
    }
}