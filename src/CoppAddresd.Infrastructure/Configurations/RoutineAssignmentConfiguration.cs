using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de asignaciones de rutinas a pacientes en el schema <c>app</c>.</summary>
public sealed class RoutineAssignmentConfiguration : IEntityTypeConfiguration<RoutineAssignment>
{
    public void Configure(EntityTypeBuilder<RoutineAssignment> builder)
    {
        builder.ToTable("routine_assignments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.RoutineId)
            .HasColumnName("routine_id");

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(AssignmentStatus.Active);

        builder.Property(x => x.Notes)
            .HasColumnName("notes");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_routine_assignments_patient_id");

        builder.HasIndex(x => x.RoutineId)
            .HasDatabaseName("ix_routine_assignments_routine_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_routine_assignments_status");

        builder.HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_routine_assignments_patient_status");

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany(x => x.RoutineAssignments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Routine)
            .WithMany()
            .HasForeignKey(x => x.RoutineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
