using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de registros emocionales (tarea <c>emocional</c>) en el schema <c>app</c>.</summary>
public sealed class EmotionalRecordConfiguration : IEntityTypeConfiguration<EmotionalRecord>
{
    public void Configure(EntityTypeBuilder<EmotionalRecord> builder)
    {
        builder.ToTable("emotional_records", "app", t =>
        {
            t.HasCheckConstraint("ck_emotional_records_mood_score_range", "\"mood_score\" BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.ProgramEnrollmentId)
            .HasColumnName("program_enrollment_id");

        builder.Property(x => x.RecordedLocalDate)
            .HasColumnName("recorded_local_date")
            .HasColumnType("date");

        builder.Property(x => x.MoodScore)
            .HasColumnName("mood_score");

        builder.Property(x => x.Barriers)
            .HasColumnName("barriers")
            .HasMaxLength(40);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_emotional_records_patient_id");

        builder.HasIndex(x => x.ProgramEnrollmentId)
            .HasDatabaseName("ix_emotional_records_program_enrollment_id");

        builder.HasIndex(x => new { x.ProgramEnrollmentId, x.RecordedLocalDate })
            .HasDatabaseName("uq_emotional_records_enrollment_date")
            .IsUnique();

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}