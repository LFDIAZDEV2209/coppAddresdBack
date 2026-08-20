using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la sede (location) en el schema <c>erp</c>.</summary>
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AddressLine1)
            .HasColumnName("address_line_1")
            .HasMaxLength(200);

        builder.Property(x => x.AddressLine2)
            .HasColumnName("address_line_2")
            .HasMaxLength(200);

        builder.Property(x => x.CityId)
            .HasColumnName("city_id");

        builder.Property(x => x.StateId)
            .HasColumnName("state_id");

        builder.Property(x => x.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(10);

        builder.Property(x => x.PhoneCountryCode)
            .HasColumnName("phone_country_code")
            .HasMaxLength(10);

        builder.Property(x => x.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.Clinic)
            .WithMany(c => c.Locations)
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        // Geografía: catálogos del schema app (misma BD). Restrict: no borrar
        // una ciudad/estado referenciada por una sede.
        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.State)
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClinicId)
            .HasDatabaseName("ix_locations_clinic_id");

        builder.HasIndex(x => x.CityId)
            .HasDatabaseName("ix_locations_city_id");

        builder.HasIndex(x => x.StateId)
            .HasDatabaseName("ix_locations_state_id");

        // Nombre único por clínica.
        builder.HasIndex(x => new { x.ClinicId, x.Name })
            .HasDatabaseName("ix_locations_clinic_name")
            .IsUnique();
    }
}
