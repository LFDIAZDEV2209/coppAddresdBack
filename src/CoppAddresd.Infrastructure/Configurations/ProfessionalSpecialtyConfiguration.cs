using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la asignación profesional ↔ especialidad en el schema <c>erp</c>.</summary>
public sealed class ProfessionalSpecialtyConfiguration : IEntityTypeConfiguration<ProfessionalSpecialty>
{
    public void Configure(EntityTypeBuilder<ProfessionalSpecialty> builder)
    {
        builder.ToTable("professional_specialties", "erp");

        builder.HasKey(x => new { x.ProfessionalId, x.SpecialtyId });

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.SpecialtyId)
            .HasColumnName("specialty_id");

        builder.Property(x => x.IsPrimary)
            .HasColumnName("is_primary");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Professional)
            .WithMany(p => p.Specialties)
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Specialty)
            .WithMany(s => s.ProfessionalSpecialties)
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SpecialtyId)
            .HasDatabaseName("ix_professional_specialties_specialty_id");
    }
}
