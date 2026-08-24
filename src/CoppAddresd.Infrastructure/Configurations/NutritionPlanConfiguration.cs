using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de planes de alimentación en el schema <c>app</c>.</summary>
public sealed class NutritionPlanConfiguration : IEntityTypeConfiguration<NutritionPlan>
{
    public void Configure(EntityTypeBuilder<NutritionPlan> builder)
    {
        builder.ToTable("nutrition_plans", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnName("description");

        builder.Property(x => x.TargetCondition)
            .HasColumnName("target_condition")
            .HasMaxLength(100);

        builder.Property(x => x.DurationDays)
            .HasColumnName("duration_days");

        builder.Property(x => x.DailyCalorieTarget)
            .HasColumnName("daily_calorie_target");

        builder.Property(x => x.IsTemplate)
            .HasColumnName("is_template")
            .HasDefaultValue(true);

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.SourcePlanId)
            .HasColumnName("source_plan_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(NutritionPlanStatus.Draft);

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
        builder.HasIndex(x => x.IsTemplate)
            .HasDatabaseName("ix_nutrition_plans_is_template");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_nutrition_plans_patient_id");

        builder.HasIndex(x => x.SourcePlanId)
            .HasDatabaseName("ix_nutrition_plans_source_plan_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_nutrition_plans_status");

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany(x => x.NutritionPlans)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SourcePlan)
            .WithMany()
            .HasForeignKey(x => x.SourcePlanId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Days)
            .WithOne(x => x.Plan)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
