using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la asignación empleado ↔ clínica en el schema <c>erp</c>.</summary>
public sealed class EmployeeClinicConfiguration : IEntityTypeConfiguration<EmployeeClinic>
{
    public void Configure(EntityTypeBuilder<EmployeeClinic> builder)
    {
        builder.ToTable("employee_clinics", "erp");

        builder.HasKey(x => new { x.EmployeeId, x.ClinicId });

        builder.Property(x => x.EmployeeId)
            .HasColumnName("employee_id");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id");

        builder.Property(x => x.IsPrimary)
            .HasColumnName("is_primary");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.ClinicAssignments)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Clinic)
            .WithMany(c => c.EmployeeClinics)
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        // La PK compuesta indexa (employee_id); este índice cubre el filtro
        // inverso: "qué empleados tiene la clínica X" (scoping de Fase 2).
        builder.HasIndex(x => x.ClinicId)
            .HasDatabaseName("ix_employee_clinics_clinic_id");
    }
}
