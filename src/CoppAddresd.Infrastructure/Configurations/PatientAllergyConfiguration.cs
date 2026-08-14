using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de alergias de paciente en el schema <c>app</c>.</summary>
public sealed class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.ToTable("patient_allergies", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.AllergenId)
            .HasColumnName("allergen_id");

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(300);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Allergen)
            .WithMany(a => a.PatientAllergies)
            .HasForeignKey(x => x.AllergenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PatientId, x.AllergenId })
            .HasDatabaseName("ix_patient_allergies_patient_allergen")
            .IsUnique();
    }
}