using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de ciudades en el schema <c>app</c>.</summary>
public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.StateId)
            .HasColumnName("state_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.State)
            .WithMany(s => s.Cities)
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StateId)
            .HasDatabaseName("ix_cities_state_id");

        builder.HasIndex(x => new { x.StateId, x.Name })
            .HasDatabaseName("ix_cities_state_name")
            .IsUnique();
    }
}