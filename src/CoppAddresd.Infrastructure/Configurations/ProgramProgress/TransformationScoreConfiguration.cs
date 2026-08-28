using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de <c>app.transformation_scores</c> (SPEC §13.1.4): histórico
/// del Índice de Transformación por semana del programa. El <c>detail</c> es
/// <c>jsonb</c> con default <c>'{}'</c>; índice de lectura
/// <c>(patient_id, week_number DESC)</c>.
/// </summary>
public sealed class TransformationScoreConfiguration : IEntityTypeConfiguration<TransformationScore>
{
    public void Configure(EntityTypeBuilder<TransformationScore> builder)
    {
        builder.ToTable("transformation_scores", "app", t =>
        {
            t.HasCheckConstraint("ck_transformation_scores_score_range", "\"score\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_transformation_scores_score_previous_range", "\"score_previous\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_transformation_scores_week_number_range", "\"week_number\" >= 1");
            t.HasCheckConstraint("ck_transformation_scores_overall_trend_values", "\"overall_trend\" IN ('up','down','stable')");
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

        builder.Property(x => x.WeekNumber)
            .HasColumnName("week_number");

        builder.Property(x => x.Detail)
            .HasColumnName("detail")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        // overall_trend en minúscula (nombres del enum = valores DB).
        builder.Property(x => x.OverallTrend)
            .HasColumnName("overall_trend")
            .HasMaxLength(10)
            .HasConversion<string>();

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

        // Indexes (SPEC §13.1.4: sin restricción única, solo el índice)
        builder.HasIndex(x => new { x.PatientId, x.WeekNumber })
            .HasDatabaseName("ix_transformation_scores_patient_week")
            .IsDescending(false, true);

        // Relationship
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}