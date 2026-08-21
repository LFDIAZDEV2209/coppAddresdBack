using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la asignación paciente ↔ profesional en el schema <c>app</c>.</summary>
public sealed class PatientProfessionalAssignmentConfiguration : IEntityTypeConfiguration<PatientProfessionalAssignment>
{
    public void Configure(EntityTypeBuilder<PatientProfessionalAssignment> builder)
    {
        builder.ToTable("patient_professionals", "app");

        builder.HasKey(x => new { x.PatientId, x.ProfessionalId });

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id");

        builder.Property(x => x.RelationshipType)
            .HasColumnName("relationship_type")
            .HasMaxLength(30)
            .HasDefaultValue("Assigned")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("Active")
            .IsRequired();

        // El actor apunta a auth.users; la FK se crea por SQL en la migración.
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.Patient)
            .WithMany(p => p.Assignments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Professional)
            .WithMany()
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Clinic)
            .WithMany()
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.SetNull);

        // El filtro principal del alcance "own": pacientes activos de un
        // profesional (listados y detalle). La PK compuesta cubre el inverso
        // (profesionales de un paciente).
        builder.HasIndex(x => new { x.ProfessionalId, x.Status })
            .HasDatabaseName("ix_patient_professionals_professional_status");
    }
}