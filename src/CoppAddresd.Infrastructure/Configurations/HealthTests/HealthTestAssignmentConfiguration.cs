using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de asignaciones de tests a pacientes en el schema <c>app</c>.</summary>
public sealed class HealthTestAssignmentConfiguration
    : IEntityTypeConfiguration<HealthTestAssignment>
{
    public void Configure(EntityTypeBuilder<HealthTestAssignment> builder)
    {
        builder.ToTable("health_test_assignments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.BatteryAssignmentId).HasColumnName("battery_assignment_id");

        builder.Property(x => x.VersionId).HasColumnName("version_id");

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestAssignmentStatus.pending)
            .HasConversion<string>();

        builder.Property(x => x.Priority).HasColumnName("priority");

        builder.Property(x => x.AssignedBy).HasColumnName("assigned_by");

        builder
            .Property(x => x.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");

        builder
            .Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");

        builder.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("timestamptz");

        builder.Property(x => x.Notes).HasColumnName("notes");

        // Indexes
        builder
            .HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_health_test_assignments_patient_id");

        builder
            .HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_health_test_assignments_patient_status");

        builder.HasIndex(x => x.VersionId).HasDatabaseName("ix_health_test_assignments_version_id");

        builder
            .HasIndex(x => x.BatteryAssignmentId)
            .HasDatabaseName("ix_health_test_assignments_battery_assignment_id");

        builder
            .HasIndex(x => new { x.Status, x.DueDate })
            .HasDatabaseName("ix_health_test_assignments_status_due");

        // Relationships
        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.BatteryAssignment)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.BatteryAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Version)
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
