using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de rutinas de ejercicio en el schema <c>app</c>.</summary>
public sealed class ExerciseRoutineConfiguration : IEntityTypeConfiguration<ExerciseRoutine>
{
    public void Configure(EntityTypeBuilder<ExerciseRoutine> builder)
    {
        builder.ToTable("exercise_routines", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnName("description");

        builder.Property(x => x.Difficulty)
            .HasColumnName("difficulty")
            .HasMaxLength(20);

        builder.Property(x => x.EstimatedMinutes)
            .HasColumnName("estimated_minutes");

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(NutritionPlanStatus.Draft);

        builder.Property(x => x.TargetMuscles)
            .HasColumnName("target_muscles");

        builder.Property(x => x.Equipment)
            .HasColumnName("equipment");

        builder.Property(x => x.WarmupNotes)
            .HasColumnName("warmup_notes");

        builder.Property(x => x.CooldownNotes)
            .HasColumnName("cooldown_notes");

        builder.Property(x => x.MediaId)
            .HasColumnName("media_id");

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
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_exercise_routines_status");

        builder.HasIndex(x => x.Category)
            .HasDatabaseName("ix_exercise_routines_category");

        // Relationships
        builder.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Exercises)
            .WithOne(x => x.Routine)
            .HasForeignKey(x => x.RoutineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
