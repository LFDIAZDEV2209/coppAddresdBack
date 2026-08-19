using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la extensión clínica (profesional) en el schema <c>erp</c>.</summary>
public sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        builder.ToTable("professionals", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(x => x.ProfessionalTypeId)
            .HasColumnName("professional_type_id");

        builder.Property(x => x.Bio)
            .HasColumnName("bio")
            .HasColumnType("text");

        builder.Property(x => x.PhotoStorageKey)
            .HasColumnName("photo_storage_key")
            .HasMaxLength(1024);

        builder.Property(x => x.OnboardingCompletedAt)
            .HasColumnName("onboarding_completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // 1:0..1 con el empleado (la FK la configura EmployeeConfiguration).
        builder.HasOne(x => x.ProfessionalType)
            .WithMany(t => t.Professionals)
            .HasForeignKey(x => x.ProfessionalTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EmployeeId)
            .HasDatabaseName("ix_professionals_employee_id")
            .IsUnique();

        builder.HasIndex(x => x.ProfessionalTypeId)
            .HasDatabaseName("ix_professionals_professional_type_id");
    }
}
