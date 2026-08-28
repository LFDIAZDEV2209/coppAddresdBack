using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de asignaciones de baterías a pacientes en el schema <c>app</c>.</summary>
public sealed class HealthTestBatteryAssignmentConfiguration
    : IEntityTypeConfiguration<HealthTestBatteryAssignment>
{
    public void Configure(EntityTypeBuilder<HealthTestBatteryAssignment> builder)
    {
        builder.ToTable("health_test_battery_assignments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.BatteryId).HasColumnName("battery_id");

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestAssignmentStatus.pending)
            .HasConversion<string>();

        builder.Property(x => x.AssignedBy).HasColumnName("assigned_by");

        builder
            .Property(x => x.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("timestamptz");

        builder
            .Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder
            .HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_health_test_battery_assignments_patient_id");

        builder
            .HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_health_test_battery_assignments_patient_status");

        builder
            .HasIndex(x => x.BatteryId)
            .HasDatabaseName("ix_health_test_battery_assignments_battery_id");

        // Relationships
        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Battery)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
