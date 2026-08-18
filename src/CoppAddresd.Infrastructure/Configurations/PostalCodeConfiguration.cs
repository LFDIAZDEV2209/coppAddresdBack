using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de códigos postales en el schema <c>app</c>.</summary>
public sealed class PostalCodeConfiguration : IEntityTypeConfiguration<PostalCode>
{
    public void Configure(EntityTypeBuilder<PostalCode> builder)
    {
        builder.ToTable("postal_codes", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CityId)
            .HasColumnName("city_id");

        builder.Property(x => x.ZipCode)
            .HasColumnName("zip_code")
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.City)
            .WithMany(c => c.PostalCodes)
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CityId)
            .HasDatabaseName("ix_postal_codes_city_id");

        builder.HasIndex(x => new { x.CityId, x.ZipCode })
            .HasDatabaseName("ix_postal_codes_city_zip")
            .IsUnique();
    }
}