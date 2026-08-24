using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de ejercicios individuales de una rutina en el schema <c>app</c>.</summary>
public sealed class RoutineExerciseConfiguration : IEntityTypeConfiguration<RoutineExercise>
{
    public void Configure(EntityTypeBuilder<RoutineExercise> builder)
    {
        builder.ToTable("routine_exercises", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RoutineId)
            .HasColumnName("routine_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnName("description");

        builder.Property(x => x.Sets)
            .HasColumnName("sets");

        builder.Property(x => x.Repetitions)
            .HasColumnName("repetitions");

        builder.Property(x => x.RestSeconds)
            .HasColumnName("rest_seconds");

        builder.Property(x => x.DurationSecs)
            .HasColumnName("duration_secs");

        builder.Property(x => x.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(6, 2);

        builder.Property(x => x.TargetMuscle)
            .HasColumnName("target_muscle");

        builder.Property(x => x.Equipment)
            .HasColumnName("equipment");

        builder.Property(x => x.Tempo)
            .HasColumnName("tempo")
            .HasMaxLength(20);

        builder.Property(x => x.Rpe)
            .HasColumnName("rpe");

        builder.Property(x => x.Tips)
            .HasColumnName("tips");

        builder.Property(x => x.MediaId)
            .HasColumnName("media_id");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => new { x.RoutineId, x.SortOrder })
            .HasDatabaseName("ix_routine_exercises_routine_sort");

        // Relationships
        builder.HasOne(x => x.Routine)
            .WithMany(x => x.Exercises)
            .HasForeignKey(x => x.RoutineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
