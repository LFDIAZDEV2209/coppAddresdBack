using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la asignación profesional ↔ sede en el schema <c>erp</c>.</summary>
public sealed class ProfessionalLocationConfiguration : IEntityTypeConfiguration<ProfessionalLocation>
{
    public void Configure(EntityTypeBuilder<ProfessionalLocation> builder)
    {
        builder.ToTable("professional_locations", "erp");

        builder.HasKey(x => new { x.ProfessionalId, x.LocationId });

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.LocationId)
            .HasColumnName("location_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Professional)
            .WithMany(p => p.LocationAssignments)
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Location)
            .WithMany(l => l.ProfessionalLocations)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LocationId)
            .HasDatabaseName("ix_professional_locations_location_id");
    }
}
