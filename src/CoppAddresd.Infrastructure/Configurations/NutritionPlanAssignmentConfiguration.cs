using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de asignaciones de planes de alimentación en el schema <c>app</c>.</summary>
public sealed class NutritionPlanAssignmentConfiguration : IEntityTypeConfiguration<NutritionPlanAssignment>
{
    public void Configure(EntityTypeBuilder<NutritionPlanAssignment> builder)
    {
        builder.ToTable("nutrition_plan_assignments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id");

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

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
            .HasDatabaseName("ix_nutrition_plan_assignments_patient_id");

        builder.HasIndex(x => x.PlanId)
            .HasDatabaseName("ix_nutrition_plan_assignments_plan_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_nutrition_plan_assignments_status");

        builder.HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_nutrition_plan_assignments_patient_status");

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
