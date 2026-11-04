using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Mapeo del catálogo de asignación en el esquema app con JSON nativo de Npgsql.</summary>
public sealed class ProgramAssignmentRecommendationConfiguration : IEntityTypeConfiguration<ProgramAssignmentRecommendation>
{
    public void Configure(EntityTypeBuilder<ProgramAssignmentRecommendation> builder)
    {
        builder.ToTable("program_assignment_recommendations", "app", t =>
            t.HasCheckConstraint("ck_program_assignment_recommendations_state",
                "state IN ('proposed', 'pending-narrative', 'applied', 'edited', 'dismissed', 'fallback')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(x => x.PatientId).HasColumnName("patient_id");
        builder.Property(x => x.AppliedBy).HasColumnName("applied_by");
        builder.Property(x => x.DismissedBy).HasColumnName("dismissed_by");
        builder.Property(x => x.ProfileSnapshot).HasColumnName("profile_snapshot").HasColumnType("jsonb");
        builder.Property(x => x.Proposal).HasColumnName("proposal").HasColumnType("jsonb");
        builder.Property(x => x.NarrativeEs).HasColumnName("narrative_es").HasColumnType("text");
        builder.Property(x => x.NarrativeEn).HasColumnName("narrative_en").HasColumnType("text");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(24).HasDefaultValue("proposed");
        builder.Property(x => x.EngineVersion).HasColumnName("engine_version").HasMaxLength(80);
        builder.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(120);
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at").HasColumnType("timestamptz");
        builder.Property(x => x.DismissedAt).HasColumnName("dismissed_at").HasColumnType("timestamptz");
        // Una recomendaci?n no desestimada por inscripción; permite conservar el historial desestimado.
        builder.HasIndex(x => x.EnrollmentId).IsUnique()
            .HasFilter("state <> 'dismissed'").HasDatabaseName("uq_program_assignment_recommendations_enrollment");
        builder.HasIndex(x => new { x.State, x.CreatedAt }).HasDatabaseName("ix_program_assignment_recommendations_state_created_at");
        builder.HasIndex(x => new { x.EnrollmentId, x.PatientId }).HasDatabaseName("ix_program_assignment_recommendations_enrollment_patient");
        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_program_assignment_recommendations_patient_id");
        builder.HasOne(x => x.Enrollment).WithMany()
            .HasForeignKey(x => new { x.EnrollmentId, x.PatientId })
            .HasPrincipalKey(x => new { x.Id, x.PatientId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CoppAddresd.Domain.Entities.PatientProfile>().WithMany()
            .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
    }
}
