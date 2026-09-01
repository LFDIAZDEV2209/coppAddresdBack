using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de evaluaciones de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestEvaluationConfiguration
    : IEntityTypeConfiguration<HealthTestEvaluation>
{
    public void Configure(EntityTypeBuilder<HealthTestEvaluation> builder)
    {
        builder.ToTable("health_test_evaluations", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AssignmentId).HasColumnName("assignment_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.VersionId).HasColumnName("version_id");

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestEvaluationStatus.started)
            .HasConversion<string>();

        builder
            .Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder
            .Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.Score).HasColumnName("score").HasColumnType("numeric(10,2)");

        builder
            .Property(x => x.ScorePercentage)
            .HasColumnName("score_percentage")
            .HasColumnType("numeric(6,2)");

        // Indexes
        builder
            .HasIndex(x => x.AssignmentId)
            .HasDatabaseName("ix_health_test_evaluations_assignment_id");

        builder
            .HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_health_test_evaluations_patient_status");

        builder
            .HasIndex(x => new { x.PatientId, x.CompletedAt })
            .HasDatabaseName("ix_health_test_evaluations_patient_completed");

        builder.HasIndex(x => x.VersionId).HasDatabaseName("ix_health_test_evaluations_version_id");

        // Relationships
        builder
            .HasOne(x => x.Assignment)
            .WithMany(x => x.Evaluations)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Version)
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
