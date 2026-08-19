using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la credencial (licencia) del profesional en el schema <c>erp</c>.</summary>
public sealed class ProfessionalLicenseConfiguration : IEntityTypeConfiguration<ProfessionalLicense>
{
    public void Configure(EntityTypeBuilder<ProfessionalLicense> builder)
    {
        builder.ToTable("professional_licenses", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id")
            .IsRequired();

        builder.Property(x => x.LicenseType)
            .HasColumnName("license_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SpecialtyId)
            .HasColumnName("specialty_id");

        builder.Property(x => x.Number)
            .HasColumnName("number")
            .HasMaxLength(100);

        builder.Property(x => x.StateId)
            .HasColumnName("state_id");

        builder.Property(x => x.Issuer)
            .HasColumnName("issuer")
            .HasMaxLength(200);

        builder.Property(x => x.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("date");

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("date");

        builder.Property(x => x.VerificationStatus)
            .HasColumnName("verification_status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Professional)
            .WithMany(p => p.Licenses)
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProfessionalId)
            .HasDatabaseName("ix_professional_licenses_professional_id");

        builder.HasIndex(x => x.SpecialtyId)
            .HasDatabaseName("ix_professional_licenses_specialty_id");

        // Única: no puede haber dos credenciales iguales del mismo tipo y número.
        builder.HasIndex(x => new { x.ProfessionalId, x.LicenseType, x.Number })
            .HasDatabaseName("ix_professional_licenses_type_number")
            .IsUnique();
    }
}
