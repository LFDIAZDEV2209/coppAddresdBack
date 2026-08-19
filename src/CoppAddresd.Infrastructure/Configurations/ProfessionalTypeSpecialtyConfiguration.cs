using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del mapeo profesión ↔ especialidad en el schema <c>erp</c>.</summary>
public sealed class ProfessionalTypeSpecialtyConfiguration : IEntityTypeConfiguration<ProfessionalTypeSpecialty>
{
    public void Configure(EntityTypeBuilder<ProfessionalTypeSpecialty> builder)
    {
        builder.ToTable("professional_type_specialties", "erp");

        builder.HasKey(x => new { x.ProfessionalTypeId, x.SpecialtyId });

        builder.Property(x => x.ProfessionalTypeId)
            .HasColumnName("professional_type_id");

        builder.Property(x => x.SpecialtyId)
            .HasColumnName("specialty_id");

        builder.HasOne(x => x.ProfessionalType)
            .WithMany(p => p.Specialties)
            .HasForeignKey(x => x.ProfessionalTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Specialty)
            .WithMany(s => s.ProfessionalTypeSpecialties)
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Cascade);

        // La PK compuesta indexa (professional_type_id); este índice cubre el
        // filtro por especialidad (consultas "qué profesiones ejercen X").
        builder.HasIndex(x => x.SpecialtyId)
            .HasDatabaseName("ix_professional_type_specialties_specialty_id");
    }
}
