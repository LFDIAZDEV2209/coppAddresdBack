using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de <c>app.health_scores</c> (SPEC §13.1.3): histórico del
/// Índice de Salud por período local. Único por
/// <c>(patient_id, period_start, period_end)</c> con índice de lectura
/// <c>(patient_id, period_end DESC)</c> y CHECKs 0..100 en todos los puntajes.
/// </summary>
public sealed class HealthScoreConfiguration : IEntityTypeConfiguration<HealthScore>
{
    public void Configure(EntityTypeBuilder<HealthScore> builder)
    {
        builder.ToTable("health_scores", "app", t =>
        {
            t.HasCheckConstraint("ck_health_scores_score_range", "\"score\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_previous_range", "\"score_previous\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_adherence_range", "\"score_adherence\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_clinical_range", "\"score_clinical\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_nutrition_range", "\"score_nutrition\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_psychology_range", "\"score_psychology\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_score_exercise_range", "\"score_exercise\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_health_scores_trend_values", "\"trend\" IN ('up','down','stable')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.Score)
            .HasColumnName("score");

        builder.Property(x => x.ScorePrevious)
            .HasColumnName("score_previous");

        builder.Property(x => x.ScoreAdherence)
            .HasColumnName("score_adherence");

        builder.Property(x => x.ScoreClinical)
            .HasColumnName("score_clinical");

        builder.Property(x => x.ScoreNutrition)
            .HasColumnName("score_nutrition");

        builder.Property(x => x.ScorePsychology)
            .HasColumnName("score_psychology");

        builder.Property(x => x.ScoreExercise)
            .HasColumnName("score_exercise");

        // trend en minúscula (nombres del enum = valores DB: up/down/stable).
        builder.Property(x => x.Trend)
            .HasColumnName("trend")
            .HasMaxLength(10)
            .HasConversion<string>();

        builder.Property(x => x.PeriodStart)
            .HasColumnName("period_start")
            .HasColumnType("date");

        builder.Property(x => x.PeriodEnd)
            .HasColumnName("period_end")
            .HasColumnType("date");

        builder.Property(x => x.CalculatedAt)
            .HasColumnName("calculated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes / constraints (SPEC §13.1.3)
        builder.HasIndex(x => new { x.PatientId, x.PeriodStart, x.PeriodEnd })
            .HasDatabaseName("uq_health_scores_patient_period")
            .IsUnique();

        builder.HasIndex(x => new { x.PatientId, x.PeriodEnd })
            .HasDatabaseName("ix_health_scores_patient_id_period_end")
            .IsDescending(false, true);

        // Relationship
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}