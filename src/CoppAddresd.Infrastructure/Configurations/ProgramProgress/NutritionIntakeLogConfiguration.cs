using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de los registros de intake nutricional en el schema <c>app</c>
/// (SPEC nutrition-intake-adherence): una fila por
/// <c>(paciente, fecha local, meal_code)</c> — único
/// <c>uq_intake_logs_patient_date_meal</c> — con la referencia al plan-day
/// resuelta server-side y la FK Restrict a <c>foodai.food_analyses</c> por la
/// columna única <c>analysis_id</c> (misma base de datos/AppDbContext; la
/// violación de FK es el backstop del chequeo de ownership D6).
/// </summary>
public sealed class NutritionIntakeLogConfiguration : IEntityTypeConfiguration<NutritionIntakeLog>
{
    public void Configure(EntityTypeBuilder<NutritionIntakeLog> builder)
    {
        builder.ToTable("nutrition_intake_logs", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.HabitCheckId)
            .HasColumnName("habit_check_id");

        builder.Property(x => x.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date");

        builder.Property(x => x.MealCode)
            .HasColumnName("meal_code")
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(x => x.Calories)
            .HasColumnName("calories");

        builder.Property(x => x.ProteinG)
            .HasColumnName("protein_g")
            .HasPrecision(8, 2);

        builder.Property(x => x.CarbsG)
            .HasColumnName("carbs_g")
            .HasPrecision(8, 2);

        builder.Property(x => x.FatG)
            .HasColumnName("fat_g")
            .HasPrecision(8, 2);

        builder.Property(x => x.FiberG)
            .HasColumnName("fiber_g")
            .HasPrecision(8, 2);

        builder.Property(x => x.WaterMl)
            .HasColumnName("water_ml");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(20)
            .HasDefaultValue("manual")
            .IsRequired();

        builder.Property(x => x.FoodAnalysisId)
            .HasColumnName("food_analysis_id");

        builder.Property(x => x.NutritionPlanId)
            .HasColumnName("nutrition_plan_id");

        builder.Property(x => x.NutritionPlanDayNumber)
            .HasColumnName("nutrition_plan_day_number");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes: búsqueda del gate por (paciente, fecha) + único del log.
        builder.HasIndex(x => new { x.PatientId, x.LocalDate })
            .HasDatabaseName("ix_intake_logs_patient_date");

        builder.HasIndex(x => new { x.PatientId, x.LocalDate, x.MealCode })
            .HasDatabaseName("uq_intake_logs_patient_date_meal")
            .IsUnique();

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HabitCheck)
            .WithMany()
            .HasForeignKey(x => x.HabitCheckId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK cross-schema app → foodai por la columna única analysis_id
        // (idempotencia pública del análisis). Restrict: no se puede borrar un
        // análisis que ya alimentó un log.
        builder.HasOne<FoodAnalysis>()
            .WithMany()
            .HasForeignKey(x => x.FoodAnalysisId)
            .HasPrincipalKey(x => x.AnalysisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<NutritionPlan>()
            .WithMany()
            .HasForeignKey(x => x.NutritionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}