using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de días/comidas de un plan de alimentación en el schema <c>app</c>.</summary>
public sealed class NutritionPlanDayConfiguration : IEntityTypeConfiguration<NutritionPlanDay>
{
    public void Configure(EntityTypeBuilder<NutritionPlanDay> builder)
    {
        builder.ToTable("nutrition_plan_days", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id");

        builder.Property(x => x.DayNumber)
            .HasColumnName("day_number");

        builder.Property(x => x.MealType)
            .HasColumnName("meal_type")
            .HasMaxLength(20);

        builder.Property(x => x.Description)
            .HasColumnName("description");

        builder.Property(x => x.Foods)
            .HasColumnName("foods");

        builder.Property(x => x.Calories)
            .HasColumnName("calories");

        builder.Property(x => x.ProteinG)
            .HasColumnName("protein_g")
            .HasPrecision(6, 2);

        builder.Property(x => x.CarbsG)
            .HasColumnName("carbs_g")
            .HasPrecision(6, 2);

        builder.Property(x => x.FatG)
            .HasColumnName("fat_g")
            .HasPrecision(6, 2);

        builder.Property(x => x.FiberG)
            .HasColumnName("fiber_g")
            .HasPrecision(6, 2);

        builder.Property(x => x.WaterMl)
            .HasColumnName("water_ml");

        builder.Property(x => x.DailyWaterMl)
            .HasColumnName("daily_water_ml")
            .HasDefaultValue(2000);

        builder.Property(x => x.Notes)
            .HasColumnName("notes");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.MediaId)
            .HasColumnName("media_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => new { x.PlanId, x.DayNumber, x.MealType })
            .HasDatabaseName("ix_nutrition_plan_days_plan_day_meal");

        // Relationships
        builder.HasOne(x => x.Plan)
            .WithMany(x => x.Days)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
